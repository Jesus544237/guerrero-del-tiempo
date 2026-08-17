using Ekkar.Core;
using UnityEngine;

namespace Ekkar.Gameplay
{
    /// <summary>
    /// La cabeza de un enemigo: vigila, persigue, pega y se muere.
    ///
    /// Es deliberadamente simple, del tamano que pide un juego de plataformas:
    /// mientras no ve a Ekkar patrulla entre dos puntos; cuando entra en su
    /// radio lo persigue; cuando lo tiene a tiro ataca con una cadencia. El
    /// golpe no hace dano al empezar la animacion sino en el fotograma del
    /// impacto, que se marca con <see cref="retardoImpacto"/>.
    ///
    /// El golpe es una caja DELANTE del enemigo, la misma que decide si puede
    /// atacar. Antes eran dos cosas distintas — un chequeo de direccion al
    /// empezar y un circulo enorme al impactar — y por eso te pegaban de
    /// espaldas: bastaba con cruzar al otro lado durante la animacion, o estar
    /// dentro de un circulo que llegaba al doble de lo que se veia.
    ///
    /// Los que vuelan ignoran el suelo y van en linea recta; los que disparan
    /// atacan desde lejos y no se acercan.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class EnemyBrain : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] float velocidad = 1.6f;
        [SerializeField] float radioPatrulla = 3f;
        [SerializeField] bool vuela = false;

        [Header("Vista y ataque")]
        [SerializeField] float radioVision = 7f;
        [SerializeField] float alcance = 1.4f;
        [SerializeField] float cadencia = 2f;
        [SerializeField] int dano = 1;
        [SerializeField] float retardoImpacto = 0.35f;   // cuando pega dentro de la animacion
        [SerializeField] float duracionAtaque = 0.8f;
        [SerializeField] bool aDistancia = false;
        [Tooltip("Solo para jefes con patrones: desde cuan lejos pueden lanzarlos. 0 = usa el alcance normal.")]
        [SerializeField] float alcanceHabilidades = 0f;
        [Tooltip("Altura del golpe: desde ahi sube y baja la caja.")]
        [SerializeField] float alturaGolpe = 0.95f;
        [SerializeField] float cajaArriba = 1.1f;
        [SerializeField] float cajaAbajo = 1.1f;

        [Header("Animacion")]
        [SerializeField] Animator animator;
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] string estadoIdle = "idle";
        [SerializeField] string estadoAndar = "caminar";
        [SerializeField] string estadoAtaque = "ataque";
        [Tooltip("Si hay varios, elige uno al azar en cada ataque.")]
        [SerializeField] string[] ataques;

        [Header("Al morir")]
        [SerializeField] float tardaEnIrse = 1.2f;
        [Tooltip("De cada cuanto suelta una reliquia que cura a Ekkar (0 a 1).")]
        [SerializeField, Range(0f, 1f)] float sueltaReliquia = 0f;
        [SerializeField] int cuantasReliquias = 1;

        Damageable _vida;
        Transform _ekkar;
        Vector2 _origen;
        float _proximoAtaque, _finAtaque, _impacto = -1f;
        int _sentido = -1;                                // los sprites miran a la izquierda
        string _estado = "";
        string _ataqueActual = "";
        BossAttackPatterns _patrones;
        bool _congelado;
        bool _viendoAEkkar;

        /// <summary>Acaba de ver a Ekkar por primera vez. Lo usa el jefe.</summary>
        public event System.Action Enfoca;

        public bool VeAEkkar => _viendoAEkkar;
        public float RadioVision => radioVision;
        public Transform Objetivo => _ekkar;

        void Awake()
        {
            _vida = GetComponent<Damageable>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
            _patrones = GetComponent<BossAttackPatterns>();
            _origen = transform.position;
            _vida.Muerto_ += AlMorir;
        }

        void OnDestroy()
        {
            if (_vida != null) _vida.Muerto_ -= AlMorir;
        }

        void Start()
        {
            var jugador = FindAnyObjectByType<EkkarController>();
            if (jugador != null) _ekkar = jugador.transform;
        }

        void Update()
        {
            if (_vida.Muerto) return;

            // ---- Ekkar ha parado el tiempo: quieto y en su pose exacta.
            // Los relojes internos se empujan hacia delante en vez de pararse,
            // para que al descongelar no se dispare de golpe todo lo que tenia
            // pendiente (el ataque que estaba a medio salir, la cadencia...).
            if (TimeControl.Detenido)
            {
                if (!_congelado)
                {
                    _congelado = true;
                    if (animator != null) animator.speed = 0f;
                }
                float dt = Time.deltaTime;
                _proximoAtaque += dt;
                if (_finAtaque > 0f) _finAtaque += dt;
                if (_impacto > 0f) _impacto += dt;
                return;
            }

            if (_congelado)
            {
                _congelado = false;
                if (animator != null) animator.speed = 1f;
            }

            // el golpe cae a mitad de la animacion, no al empezarla
            if (_impacto > 0f && Time.time >= _impacto)
            {
                _impacto = -1f;
                Impactar();
            }

            if (Time.time < _finAtaque) return;            // sigue en plena estocada

            float dx = _ekkar != null ? _ekkar.position.x - transform.position.x : 999f;
            float dist = _ekkar != null ? Vector2.Distance(_ekkar.position, transform.position) : 999f;

            bool ve = dist <= radioVision;
            if (ve && !_viendoAEkkar)
            {
                _viendoAEkkar = true;
                Enfoca?.Invoke();
            }
            else if (!ve) _viendoAEkkar = false;

            if (ve)
            {
                Mirar(dx < 0f ? -1 : 1);

                if (Time.time >= _proximoAtaque && PuedeGolpear())
                {
                    Atacar();
                    return;
                }

                if (!aDistancia && Mathf.Abs(dx) > alcance * 0.65f)
                {
                    Mover(_sentido);
                    Play(estadoAndar);
                    return;
                }

                Play(estadoIdle);
                return;
            }

            Patrullar();
        }

        void Impactar()
        {
            // los jefes tienen su propio repertorio: cada animacion saca una
            // cosa distinta. Si el patron se ha encargado, no hay mandoble
            if (_patrones != null && _patrones.Ejecuta(_ataqueActual, _sentido, Pecho)) return;

            if (aDistancia)
            {
                // la saeta sale de la altura del pecho, hacia donde mira
                Vector3 boca = transform.position + new Vector3(_sentido * 0.6f, alturaGolpe, 0f);
                Projectile.Lanzar(boca, new Vector2(_sentido, 0f),
                                  Damageable.Bando.Enemigo, dano);
                return;
            }

            // se vuelve a mirar en el fotograma del impacto: si Ekkar se ha
            // quitado de en medio durante la animacion, el golpe da al aire
            Damageable.GolpearFrente(Pecho, _sentido, alcance, cajaArriba, cajaAbajo,
                                     Damageable.Bando.Enemigo, dano);
        }

        Vector2 Pecho => (Vector2)transform.position + new Vector2(0f, alturaGolpe);

        /// <summary>
        /// Un enemigo solo pega a lo que tiene DELANTE y a su altura, y la caja
        /// que se comprueba aqui es exactamente la misma que reparte el dano.
        /// </summary>
        bool PuedeGolpear()
        {
            if (_ekkar == null) return false;

            // un jefe con patrones no necesita tenerlo pegado: sus agujas, su
            // reloj y sus grietas llegan desde lejos. Lo unico que pide es
            // tenerlo por delante
            if (_patrones != null && alcanceHabilidades > 0.01f)
            {
                float dxx = _ekkar.position.x - transform.position.x;
                if (Mathf.Abs(dxx) > alcanceHabilidades) return false;
                if (Mathf.Abs(_ekkar.position.y - transform.position.y) > alcanceHabilidades) return false;
                return Mathf.Abs(dxx) < 0.4f || Mathf.Sign(dxx) == _sentido;
            }

            if (aDistancia)
            {
                // el que dispara solo necesita tenerlo delante y a tiro
                float dx = _ekkar.position.x - transform.position.x;
                float dy = _ekkar.position.y - transform.position.y;
                if (Mathf.Abs(dx) > alcance || Mathf.Abs(dy) > cajaArriba * 2f) return false;
                return Mathf.Abs(dx) < 0.25f || Mathf.Sign(dx) == _sentido;
            }

            var objetivo = _ekkar.GetComponent<Damageable>();
            if (objetivo == null || objetivo.Muerto) return false;

            var caja = Damageable.CajaFrente(Pecho, _sentido, alcance, cajaArriba, cajaAbajo);
            return objetivo.DentroDe(caja);
        }

        void Patrullar()
        {
            if (radioPatrulla <= 0.01f) { Play(estadoIdle); return; }

            float desde = transform.position.x - _origen.x;
            if (desde < -radioPatrulla) Mirar(1);
            else if (desde > radioPatrulla) Mirar(-1);

            Mover(_sentido);
            Play(estadoAndar);
        }

        void Mover(int sentido)
        {
            if (velocidad <= 0.001f) return;

            Vector3 paso = new Vector3(sentido * velocidad * Time.deltaTime, 0f, 0f);
            if (vuela && _ekkar != null)
            {
                // los que flotan se acercan tambien en vertical, sin suelo
                float dy = _ekkar.position.y + 0.6f - transform.position.y;
                paso.y = Mathf.Clamp(dy, -1f, 1f) * velocidad * 0.6f * Time.deltaTime;
            }
            transform.position += paso;
        }

        void Mirar(int sentido)
        {
            _sentido = sentido;
            // el arte viene mirando a la izquierda: se voltea para mirar a la derecha
            if (sprite != null) sprite.flipX = sentido > 0;
        }

        void Atacar()
        {
            _proximoAtaque = Time.time + cadencia;
            _finAtaque = Time.time + duracionAtaque;
            _impacto = Time.time + retardoImpacto;
            _ataqueActual = EligeAtaque();
            Play(_ataqueActual, true);
            Audio.Sfx.Play("espada", 0.5f, Random.Range(0.85f, 1.15f));
        }

        /// <summary>
        /// Un ataque al azar de los que tenga. Los jefes traen varios para que
        /// la pelea no sea el mismo golpe repetido treinta veces.
        /// </summary>
        string EligeAtaque()
        {
            if (ataques == null || ataques.Length == 0) return estadoAtaque;
            return ataques[Random.Range(0, ataques.Length)];
        }

        // -------------------------------------------------------- de fuera

        /// <summary>Lo deja clavado un rato, sin atacar ni moverse.</summary>
        public void Paraliza(float segundos)
        {
            if (segundos <= 0f) return;
            _finAtaque = Time.time + segundos;
            _proximoAtaque = Time.time + segundos;
            _impacto = -1f;                       // se anula el golpe a medias
        }

        /// <summary>
        /// Le cambia el repertorio entero. Lo usa el jefe al transformarse:
        /// otra pose de reposo, otros ataques y otro ritmo.
        /// </summary>
        public void CambiaDeFase(string idle, string[] nuevosAtaques,
                                 float nuevaCadencia, float nuevaVelocidad)
        {
            if (!string.IsNullOrEmpty(idle)) { estadoIdle = idle; estadoAndar = idle; }
            if (nuevosAtaques != null && nuevosAtaques.Length > 0)
            {
                ataques = nuevosAtaques;
                estadoAtaque = nuevosAtaques[0];
            }
            if (nuevaCadencia > 0.05f) cadencia = nuevaCadencia;
            if (nuevaVelocidad > 0f) velocidad = nuevaVelocidad;
            _estado = "";                          // fuerza el cambio de pose
        }

        /// <summary>Reproduce una animacion concreta y bloquea ese rato.</summary>
        public void Interpreta(string estado, float segundos)
        {
            Paraliza(segundos);
            Play(estado, true);
        }

        void Play(string estado, bool forzar = false)
        {
            if (animator == null || (!forzar && _estado == estado)) return;
            if (animator.runtimeAnimatorController == null) return;
            if (!animator.HasState(0, Animator.StringToHash(estado)))
            {
                Debug.LogWarning($"[Ekkar] {name}: no existe el estado '{estado}' en su controlador", this);
                return;
            }
            _estado = estado;
            animator.Play(estado, 0, 0f);
        }

        void AlMorir(Damageable _)
        {
            enabled = false;
            if (animator != null) animator.speed = 1f;

            // lo que deja al caer: la unica forma que tiene Ekkar de recuperar
            // vida sin volver a una hoguera
            if (Random.value < sueltaReliquia)
                for (int i = 0; i < Mathf.Max(1, cuantasReliquias); i++)
                    HealthPickup.Suelta(transform.position + Vector3.up * 0.6f);

            Destroy(gameObject, tardaEnIrse);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.13f, 0.83f, 0.93f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radioVision);

            var caja = Damageable.CajaFrente(Pecho, _sentido == 0 ? -1 : _sentido,
                                             alcance, cajaArriba, cajaAbajo);
            Gizmos.color = new Color(0.86f, 0.15f, 0.15f, 0.8f);
            Gizmos.DrawWireCube(caja.center, new Vector3(caja.width, caja.height, 0.1f));
        }
    }
}
