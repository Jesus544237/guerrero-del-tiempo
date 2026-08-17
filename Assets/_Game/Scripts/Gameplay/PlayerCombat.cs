using UnityEngine;

namespace Ekkar.Gameplay
{
    /// <summary>
    /// El lado ofensivo de Ekkar y el enganche con lo que ya existia: cuando se
    /// queda sin vida llama a <see cref="PlayerRespawn"/>, que es quien sabe
    /// devolverlo a la ultima hoguera.
    ///
    /// El golpe no sale al pulsar sino un poco despues, en el fotograma en que
    /// la espada esta extendida, igual que en los enemigos. Va aparte de
    /// EkkarController a proposito: ese se ocupa de moverlo y de la animacion,
    /// y este solo de repartir y encajar.
    ///
    /// El mana es la bolsa de las habilidades y nada mas. Antes el autoataque
    /// se la bebia solo al llegar arriba, que era justo lo contrario de lo que
    /// espera cualquiera: llenas la barra peleando y de repente desaparece sin
    /// haber pedido nada. Ahora el golpe cargado tiene su propia tecla y su
    /// propio precio, y pegar normal nunca resta.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Espada")]
        [SerializeField] int dano = 1;
        [Tooltip("Cuanto llega la espada por delante del centro de Ekkar.")]
        [SerializeField] float alcance = 1.5f;
        [Tooltip("Altura del pecho: desde ahi se mide la caja del golpe.")]
        [SerializeField] float alturaGolpe = 1.05f;
        [SerializeField] float cajaArriba = 0.85f;
        [SerializeField] float cajaAbajo = 0.95f;
        [SerializeField] float retardoImpacto = 0.16f;
        [SerializeField] float cadencia = 0.42f;

        [Header("Mana")]
        [Tooltip("Cuanto sube por golpe acertado.")]
        [SerializeField] int manaPorGolpe = 1;
        [SerializeField] int manaMaximo = 10;
        [Tooltip("Con cuanto arranca el nivel y con cuanto vuelve al reaparecer.")]
        [SerializeField] int manaInicial = 4;

        [Header("Golpe cargado")]
        [SerializeField] int costeCargado = 4;
        [SerializeField] int danoCargado = 3;
        [SerializeField] float alcanceCargado = 2.2f;
        [Tooltip("Vida que devuelve el golpe cargado cuando acierta.")]
        [SerializeField] int curaCargado = 1;

        [Header("Referencias")]
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] PlayerRespawn respawn;
        [SerializeField] AudioClip sonidoEspada;

        Damageable _vida;
        float _proximo, _impacto = -1f;
        int _mana;
        // las 5 formas del golpe cargado, en orden. Empieza en 0 porque se
        // avanza al lanzar: asi el primer cargado sale con la forma 1 y no con
        // la 2, que era lo que pasaba al avanzar antes de elegir animacion
        int _forma;
        bool _cargadoEnCurso;
        int _sentidoGolpe = 1;       // hacia donde miraba al lanzar, no al impactar

        public int Mana => _mana;
        public int ManaMaximo => manaMaximo;
        public int CosteCargado => costeCargado;

        /// <summary>Hay mana de sobra para soltar el golpe cargado.</summary>
        public bool GolpeCargadoListo => _mana >= costeCargado;

        public event System.Action<int, int> ManaCambio;

        /// <summary>Salta cuando se pide algo que no se puede pagar.</summary>
        public event System.Action SinMana;

        void Awake()
        {
            _vida = GetComponent<Damageable>();
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
            if (respawn == null) respawn = GetComponent<PlayerRespawn>();
            _vida.Muerto_ += AlMorir;
            _mana = Mathf.Clamp(manaInicial, 0, manaMaximo);
        }

        void Start()
        {
            // el HUD se construye en su propio Start: se le avisa aqui para que
            // la barra no arranque vacia aunque Ekkar ya tenga mana
            Avisa();
        }

        void OnDestroy()
        {
            if (_vida != null) _vida.Muerto_ -= AlMorir;
        }

        void Update()
        {
            if (_vida.Muerto) return;
            if (_impacto <= 0f || Time.time < _impacto) return;

            _impacto = -1f;

            int fuerza = _cargadoEnCurso ? danoCargado : dano;
            float largo = _cargadoEnCurso ? alcanceCargado : alcance;
            float extra = _cargadoEnCurso ? 0.35f : 0f;

            Vector2 origen = (Vector2)transform.position + new Vector2(0f, alturaGolpe);
            int tocados = Damageable.GolpearFrente(origen, _sentidoGolpe, largo,
                                                   cajaArriba + extra, cajaAbajo + extra,
                                                   Damageable.Bando.Jugador, fuerza);

            if (tocados > 0)
            {
                if (sonidoEspada != null) Audio.Sfx.Reproducir(sonidoEspada, 0.8f);
                else Audio.Sfx.Play("impacto", 0.7f);

                // pegar llena la bolsa; nunca la vacia
                if (!_cargadoEnCurso) Suma(manaPorGolpe * Mathf.Min(tocados, 2));
                else
                {
                    // el golpe cargado le devuelve un pedazo de vida: cuesta
                    // cuatro de mana y hay que acertarlo, asi que jugarsela
                    // pegando de cerca tiene premio
                    int puesto = _vida.Curar(curaCargado);
                    if (puesto > 0)
                    {
                        Audio.Sfx.Play("mana", 0.7f, 1.5f);
                        UI.PlayerHealthBar.Aviso($"+{puesto}", new Color(0.35f, 0.98f, 0.62f));
                    }
                }
            }

            _cargadoEnCurso = false;
        }

        /// <summary>
        /// La llama EkkarController al lanzar el ataque. Devuelve false si el
        /// golpe no ha salido (todavia en cadencia, o sin mana para el cargado)
        /// para que el controlador no reproduzca la animacion en balde.
        /// </summary>
        public bool LanzarGolpe(bool cargado)
        {
            if (Time.time < _proximo) return false;

            if (cargado)
            {
                if (!GolpeCargadoListo) { SinMana?.Invoke(); return false; }
                // se paga al lanzar, no al acertar: fallar tiene precio
                GastarMana(costeCargado);
                _forma = _forma >= 5 ? 1 : _forma + 1;
            }

            _cargadoEnCurso = cargado;
            _sentidoGolpe = Sentido;
            _proximo = Time.time + cadencia;
            _impacto = Time.time + retardoImpacto;
            return true;
        }

        /// <summary>+1 mira a la derecha, -1 a la izquierda.</summary>
        public int Sentido => sprite != null && sprite.flipX ? -1 : 1;

        /// <summary>
        /// Que animacion toca en el golpe cargado. Las cinco formas van en orden
        /// en vez de al azar: asi un combo cargado se siente como que crece, y
        /// el jugador reconoce en que punto esta.
        /// </summary>
        public string AnimacionCargada(bool enSuelo)
        {
            if (!enSuelo) return "salto_vertical_carga_mana";
            return $"autoataque_carga_mana_forma_{Mathf.Clamp(_forma, 1, 5)}";
        }

        /// <summary>Intenta pagar. Si no llega, avisa y no descuenta nada.</summary>
        public bool Paga(int cuanto)
        {
            if (cuanto <= 0) return true;
            if (_mana < cuanto) { SinMana?.Invoke(); return false; }
            GastarMana(cuanto);
            return true;
        }

        public void GastarMana(int cuanto)
        {
            if (cuanto <= 0) return;
            _mana = Mathf.Max(0, _mana - cuanto);
            Avisa();
        }

        void Suma(int cuanto)
        {
            int antes = _mana;
            _mana = Mathf.Min(manaMaximo, _mana + Mathf.Max(0, cuanto));
            if (_mana != antes) Avisa();
        }

        void Avisa() => ManaCambio?.Invoke(_mana, manaMaximo);

        void AlMorir(Damageable _)
        {
            if (respawn != null) respawn.Die();
        }

        /// <summary>La llama PlayerRespawn cuando lo devuelve a la hoguera.</summary>
        public void Revivir()
        {
            _vida.Revivir();
            _proximo = 0f;
            _impacto = -1f;
            _cargadoEnCurso = false;
            // reaparecer con la barra a cero dejaba a Ekkar indefenso justo en
            // el sitio donde acababa de morir
            _mana = Mathf.Clamp(manaInicial, 0, manaMaximo);
            Avisa();
        }

        void OnDrawGizmosSelected()
        {
            Vector2 origen = (Vector2)transform.position + new Vector2(0f, alturaGolpe);
            var caja = Damageable.CajaFrente(origen, Sentido, alcance, cajaArriba, cajaAbajo);
            Gizmos.color = new Color(0.98f, 0.75f, 0.14f, 0.85f);
            Gizmos.DrawWireCube(caja.center, new Vector3(caja.width, caja.height, 0.1f));

            var carga = Damageable.CajaFrente(origen, Sentido, alcanceCargado,
                                              cajaArriba + 0.35f, cajaAbajo + 0.35f);
            Gizmos.color = new Color(0.13f, 0.83f, 0.93f, 0.5f);
            Gizmos.DrawWireCube(carga.center, new Vector3(carga.width, carga.height, 0.1f));
        }
    }
}
