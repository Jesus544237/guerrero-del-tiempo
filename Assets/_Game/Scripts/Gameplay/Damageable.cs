using System;
using UnityEngine;

namespace Ekkar.Gameplay
{
    /// <summary>
    /// Todo lo que puede recibir golpes: Ekkar y los enemigos. Lleva la vida,
    /// los fotogramas de invulnerabilidad para que un solo golpe no cuente
    /// tres veces, y avisa a quien quiera escuchar cuando le hacen dano o cae.
    ///
    /// No decide que pasa despues: de eso se encargan EnemyBrain (desaparecer)
    /// y PlayerCombat (llamar a PlayerRespawn). Asi el mismo componente vale
    /// para los dos bandos.
    /// </summary>
    public class Damageable : MonoBehaviour
    {
        public enum Bando { Jugador, Enemigo }

        [Header("Bando")]
        [SerializeField] Bando bando = Bando.Enemigo;

        [Header("Vida")]
        [SerializeField] int vidaMaxima = 3;
        [SerializeField] float invulnerable = 0.35f;

        [Header("Animacion")]
        [SerializeField] Animator animator;
        [SerializeField] string estadoDano = "dano";
        [SerializeField] string estadoMuerte = "muerte";
        [SerializeField] SpriteRenderer parpadeo;

        [Header("Sonido")]
        [SerializeField] AudioClip sonidoDano;
        [SerializeField] AudioClip sonidoMuerte;

        float _sinDanoHasta;
        int _vida;

        public Bando Lado => bando;
        public bool Muerto { get; private set; }
        public int Vida => _vida;
        public int VidaMaxima => vidaMaxima;

        /// <summary>Le han dado. El int es la vida que queda.</summary>
        public event Action<Damageable, int> Danado;
        public event Action<Damageable> Muerto_;

        /// <summary>
        /// Se pregunta justo antes de caer: si alguien contesta que si, no
        /// muere. Lo usan los jefes por fases, que en vez de morir se
        /// transforman y vuelven con la vida entera.
        /// </summary>
        public event Func<Damageable, bool> Aguanta;

        void Awake()
        {
            _vida = vidaMaxima;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (parpadeo == null) parpadeo = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// Devuelve true solo si el golpe ha entrado de verdad, para que quien
        /// pega sepa si tiene que sonar el impacto.
        /// </summary>
        public bool RecibirGolpe(int dano, Vector2 desde, bool saltaInvulnerable = false)
        {
            if (Muerto || dano <= 0) return false;
            if (!saltaInvulnerable && Time.time < _sinDanoHasta) return false;

            _vida = Mathf.Max(0, _vida - dano);
            _sinDanoHasta = Time.time + invulnerable;

            if (_vida == 0 && Aguanta != null && Aguanta(this))
            {
                // alguien lo ha sostenido en pie: se queda con lo justo y quien
                // haya respondido se encarga de lo que venga despues
                _vida = 1;
                Reproducir(sonidoDano);
                Danado?.Invoke(this, _vida);
                return true;
            }

            if (_vida == 0)
            {
                Muerto = true;
                Reproducir(sonidoMuerte);
                Play(estadoMuerte);
                foreach (var col in GetComponentsInChildren<Collider2D>())
                    col.enabled = false;
                Muerto_?.Invoke(this);
            }
            else
            {
                Reproducir(sonidoDano);
                Play(estadoDano);
                if (parpadeo != null) StartCoroutine(Destello());
            }

            Danado?.Invoke(this, _vida);
            return true;
        }

        /// <summary>Devuelve cuanta vida ha entrado de verdad.</summary>
        public int Curar(int cantidad)
        {
            if (Muerto || cantidad <= 0) return 0;
            int antes = _vida;
            _vida = Mathf.Min(vidaMaxima, _vida + cantidad);
            int puesto = _vida - antes;
            // avisar aunque sea para bien: la barra escucha por aqui, y sin
            // esto curarse no se veia en pantalla
            if (puesto > 0) Danado?.Invoke(this, _vida);
            return puesto;
        }

        /// <summary>Le devuelve toda la vida. Lo usa el jefe al cambiar de fase.</summary>
        public void CuraDelTodo()
        {
            if (Muerto) return;
            _vida = vidaMaxima;
            Danado?.Invoke(this, _vida);
        }

        /// <summary>Unos segundos sin poder recibir dano.</summary>
        public void HazteInmune(float segundos)
        {
            if (segundos <= 0f) return;
            _sinDanoHasta = Mathf.Max(_sinDanoHasta, Time.time + segundos);
        }

        /// <summary>Vuelve a la vida entero. Lo usa el reaparecer de Ekkar.</summary>
        public void Revivir()
        {
            Muerto = false;
            _vida = vidaMaxima;
            _sinDanoHasta = 0f;
            foreach (var col in GetComponentsInChildren<Collider2D>())
                col.enabled = true;
            if (parpadeo != null) parpadeo.color = Color.white;
        }

        void Play(string estado)
        {
            if (animator == null || string.IsNullOrEmpty(estado)) return;
            if (animator.runtimeAnimatorController == null) return;
            if (!animator.HasState(0, Animator.StringToHash(estado))) return;
            animator.Play(estado, 0, 0f);
        }

        void Reproducir(AudioClip clip)
        {
            // el audio 2D suena siempre igual de fuerte; PlayClipAtPoint lo
            // colocaba en el mundo y con la camara lejos no se oia nada.
            // Y se salta el silencio del principio del wav, que era lo que hacia
            // que el quejido llegara despues del golpe.
            if (clip != null) Audio.Sfx.ReproducirDesdeElSonido(clip, 1f);
            else Audio.Sfx.Play(Muerto ? "muerte" : "dano", 0.85f);
        }

        System.Collections.IEnumerator Destello()
        {
            var original = parpadeo.color;
            parpadeo.color = new Color(1f, 0.5f, 0.5f, original.a);
            yield return new WaitForSeconds(0.09f);
            if (parpadeo != null) parpadeo.color = original;
        }

        /// <summary>
        /// Le pega a todo lo del bando contrario dentro del circulo. Solo para
        /// lo que de verdad revienta en todas direcciones, como el chronobreak;
        /// para los golpes normales esta <see cref="GolpearFrente"/>.
        /// </summary>
        public static int Golpear(Vector2 centro, float radio, Bando atacante, int dano)
        {
            int tocados = 0;
            foreach (var col in Physics2D.OverlapCircleAll(centro, radio))
            {
                var d = col.GetComponentInParent<Damageable>();
                if (d == null || d.Muerto || d.Lado == atacante) continue;
                if (d.RecibirGolpe(dano, centro)) tocados++;
            }
            return tocados;
        }

        /// <summary>
        /// El golpe cuerpo a cuerpo de verdad: una caja DELANTE de quien pega,
        /// con su alcance y su altura.
        ///
        /// Antes esto era un circulo, y de ahi venian las dos quejas a la vez:
        /// contra un bicho de colisionador alto entraba desde muy lejos o desde
        /// debajo, y contra uno bajito no entraba aunque lo tuvieras pegado. La
        /// caja se lee igual que se ve, y ademas no puede alcanzar a nadie que
        /// este a la espalda, porque la espalda no esta dentro de la caja.
        ///
        /// El pequeño margen hacia atras es para que un enemigo literalmente
        /// pegado al cuerpo siga contando: si no, al solaparse los dos sprites
        /// el golpe pasaba de largo.
        /// </summary>
        /// <param name="origen">Punto del pecho de quien pega, en el mundo.</param>
        /// <param name="sentido">+1 mira a la derecha, -1 a la izquierda.</param>
        /// <param name="alcance">Cuanto llega por delante.</param>
        /// <param name="arriba">Cuanto sube la caja desde el origen.</param>
        /// <param name="abajo">Cuanto baja la caja desde el origen.</param>
        public static int GolpearFrente(Vector2 origen, int sentido, float alcance,
                                        float arriba, float abajo, Bando atacante, int dano,
                                        float margenAtras = 0.25f)
        {
            Rect caja = CajaFrente(origen, sentido, alcance, arriba, abajo, margenAtras);

            int tocados = 0;
            foreach (var col in Physics2D.OverlapAreaAll(caja.min, caja.max))
            {
                var d = col.GetComponentInParent<Damageable>();
                if (d == null || d.Muerto || d.Lado == atacante) continue;
                if (d.RecibirGolpe(dano, origen)) tocados++;
            }
            return tocados;
        }

        /// <summary>
        /// Si hay algo del bando contrario dentro del radio, sin tocarlo.
        ///
        /// Sirve para no cobrar una habilidad que no va a hacer nada: la
        /// tormenta del salto doble se paga, y cobrarla por saltar en campo
        /// abierto vaciaba la barra solo por moverse.
        /// </summary>
        public static bool HayObjetivo(Vector2 centro, float radio, Bando atacante)
        {
            foreach (var col in Physics2D.OverlapCircleAll(centro, radio))
            {
                var d = col.GetComponentInParent<Damageable>();
                if (d != null && !d.Muerto && d.Lado != atacante) return true;
            }
            return false;
        }

        /// <summary>
        /// El chronobreak: no mata, deshuesa.
        ///
        /// A todo lo que pilla alrededor le arranca la vida hasta dejarlo en los
        /// huesos, y solo remata a quien ya venia herido. Antes hacia 999 de
        /// dano, o sea que limpiaba la pantalla de un botonazo: una definitiva
        /// que gana la pelea sola no es una definitiva, es un boton de saltarse
        /// el nivel.
        /// </summary>
        /// <param name="dejaEn">Fraccion de vida en la que se queda el que sobrevive.</param>
        /// <param name="umbralRemate">Por debajo de esta fraccion, cae.</param>
        /// <returns>Cuantos han caido y cuantos han quedado tocados.</returns>
        public static (int caidos, int tocados) Chronobreak(Vector2 centro, float radio, Bando atacante,
                                                            float dejaEn = 0.22f, float umbralRemate = 0.30f)
        {
            // A un jefe no se le deshuesa: al ir por fraccion de su barra, el
            // boton le quitaba 18 de sus 24 de una vez, con lo que darle mas
            // vida no lo hacia mas duro — solo hacia el numero mas grande. Se
            // lleva un mordisco fijo, que sigue siendo el doble que un golpe
            // cargado y le puede cerrar la fase si venia justo.
            const int MordiscoAJefe = 6;

            int caidos = 0, tocados = 0;
            var vistos = new System.Collections.Generic.HashSet<Damageable>();

            foreach (var col in Physics2D.OverlapCircleAll(centro, radio))
            {
                var d = col.GetComponentInParent<Damageable>();
                if (d == null || d.Muerto || d.Lado == atacante) continue;
                if (!vistos.Add(d)) continue;          // un bicho con dos colliders es un bicho

                bool esJefe = d.GetComponent<BossEncounter>() != null;

                int limite = Mathf.Max(1, Mathf.CeilToInt(d.vidaMaxima * umbralRemate));
                bool remata = !esJefe && d._vida <= limite;

                int golpe = esJefe
                    ? Mathf.Min(d._vida, MordiscoAJefe)
                    : remata
                        ? d._vida
                        : d._vida - Mathf.Max(1, Mathf.CeilToInt(d.vidaMaxima * dejaEn));

                if (golpe <= 0) continue;
                if (!d.RecibirGolpe(golpe, centro, saltaInvulnerable: true)) continue;

                // un jefe nunca entra por "remata", pero puede caer igual si el
                // mordisco se lleva lo que le quedaba
                if (remata || d.Muerto) caidos++;
                else tocados++;
            }
            return (caidos, tocados);
        }

        /// <summary>La misma caja que usa el golpe, para dibujarla y comprobarla.</summary>
        public static Rect CajaFrente(Vector2 origen, int sentido, float alcance,
                                      float arriba, float abajo, float margenAtras = 0.25f)
        {
            if (sentido == 0) sentido = 1;
            float delante = origen.x + sentido * alcance;
            float detras = origen.x - sentido * margenAtras;
            float xMin = Mathf.Min(delante, detras);
            float xMax = Mathf.Max(delante, detras);
            return Rect.MinMaxRect(xMin, origen.y - abajo, xMax, origen.y + arriba);
        }

        /// <summary>
        /// Si este objetivo cae dentro de la caja. Sirve para decidir si ya se
        /// puede atacar sin tener que lanzar el golpe a ver que pasa.
        /// </summary>
        public bool DentroDe(Rect caja)
        {
            foreach (var col in GetComponentsInChildren<Collider2D>())
            {
                if (col == null || !col.enabled) continue;
                var b = col.bounds;
                if (b.max.x >= caja.xMin && b.min.x <= caja.xMax &&
                    b.max.y >= caja.yMin && b.min.y <= caja.yMax) return true;
            }
            return false;
        }
    }
}
