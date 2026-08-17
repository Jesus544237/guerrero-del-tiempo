using Ekkar.UI;
using UnityEngine;

namespace Ekkar.Gameplay
{
    /// <summary>
    /// Convierte a un enemigo con mucha vida en una pelea de jefe de verdad.
    ///
    /// Hasta ahora un jefe era un enemigo normal con el numero de vida mas
    /// alto: entrabas, le pegabas y lo unico que sonaba era su quejido al
    /// recibir, que empezaba y paraba con tus golpes. De ahi la sensacion de
    /// que "el sonido del jefe sale cuando le pegas y se quita cuando dejas de
    /// pegarle": no habia nada mas que sonara.
    ///
    /// Esto pone lo que faltaba alrededor: se anuncia al entrar en su terreno,
    /// arranca su tema y aparta el ambiente de la era, saca una barra de vida
    /// grande arriba, y cada tercio de vida marca una fase con su fogonazo y su
    /// cambio de escenario. Al caer, todo se retira y vuelve el ambiente.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class BossEncounter : MonoBehaviour
    {
        [Header("Presentacion")]
        [SerializeField] string nombre = "EL SENOR DEL TIEMPO";
        [SerializeField] string subtitulo = "El fin de todas las eras";
        [Tooltip("A que distancia de Ekkar empieza el combate.")]
        [SerializeField] float radioEntrada = 13f;

        [Header("Fases")]
        [Tooltip("Cuantas barras de vida hay que vaciarle en total.")]
        [SerializeField] int fases = 2;
        [Tooltip("Cada fase mueve el escenario del jefe final.")]
        [SerializeField] bool dirigeElEscenario = true;

        [Header("La transformacion")]
        [SerializeField] string animTransformacion = "transformacion_fase2";
        [SerializeField] float duracionTransformacion = 1.15f;
        [SerializeField] string idleSiguienteFase = "idle_fase2";
        [SerializeField] string[] ataquesSiguienteFase;
        [SerializeField] float cadenciaSiguienteFase = 2.6f;
        [SerializeField] float velocidadSiguienteFase = 2.1f;

        [Header("Musica")]
        [SerializeField] bool musicaPropia = true;
        [Tooltip("Cuanto se aparta el ambiente de la era mientras dura.")]
        [SerializeField, Range(0f, 1f)] float agachaAmbiente = 0.15f;

        Damageable _vida;
        EnemyBrain _cerebro;
        Transform _ekkar;
        BossBanner _cartel;
        Audio.BossMusic _musica;
        Audio.LevelMusic _ambiente;
        FX.BossPhaseDirector _escenario;
        int _fase = 1;
        bool _empezado, _terminado, _transformando;

        void Awake()
        {
            _vida = GetComponent<Damageable>();
            _cerebro = GetComponent<EnemyBrain>();
            _vida.Danado += AlRecibir;
            _vida.Muerto_ += AlCaer;
            _vida.Aguanta += ImpideLaMuerte;
        }

        void OnDestroy()
        {
            if (_vida == null) return;
            _vida.Danado -= AlRecibir;
            _vida.Muerto_ -= AlCaer;
            _vida.Aguanta -= ImpideLaMuerte;
        }

        void Start()
        {
            var jugador = FindAnyObjectByType<EkkarController>();
            if (jugador != null) _ekkar = jugador.transform;

            _ambiente = FindAnyObjectByType<Audio.LevelMusic>();
            if (dirigeElEscenario) _escenario = FindAnyObjectByType<FX.BossPhaseDirector>();

            if (musicaPropia) MontaMusica();

            // el cerebro tambien avisa al ver a Ekkar: lo que llegue antes
            var cerebro = GetComponent<EnemyBrain>();
            if (cerebro != null) cerebro.Enfoca += Empieza;
        }

        void Update()
        {
            if (_empezado || _terminado || _ekkar == null) return;
            if (Vector2.Distance(_ekkar.position, transform.position) <= radioEntrada) Empieza();
        }

        /// <summary>
        /// La musica vive fuera del jefe a proposito: el jefe se destruye poco
        /// despues de caer y se llevaria por delante el fundido de salida.
        /// </summary>
        void MontaMusica()
        {
            var go = new GameObject("_MusicaJefe");
            go.AddComponent<AudioSource>();
            _musica = go.AddComponent<Audio.BossMusic>();
        }

        // ------------------------------------------------------------ pelea

        void Empieza()
        {
            if (_empezado || _terminado || _vida.Muerto) return;
            _empezado = true;

            var fuente = FindAnyObjectByType<PlayerHealthBar>();
            _cartel = BossBanner.Crea(fuente != null ? fuente.fuente : null);
            _cartel.Presenta(nombre, subtitulo);
            _cartel.MuestraBarra(nombre);
            _cartel.Vida(1f);
            _cartel.Fase(1, fases);

            // el cartelito sobre la cabeza sobra: ya tiene barra grande arriba
            var etiqueta = GetComponent<NameTag>();
            if (etiqueta != null) etiqueta.Oculta();

            if (_musica != null) _musica.Empieza();
            if (_ambiente != null) _ambiente.Apartate(agachaAmbiente);

            Audio.Sfx.Play("chrono", 0.9f, 0.45f);
        }

        void AlRecibir(Damageable d, int queda)
        {
            if (!_empezado) Empieza();
            if (_cartel == null) return;

            _cartel.Vida(d.VidaMaxima > 0 ? queda / (float)d.VidaMaxima : 0f);
        }

        /// <summary>
        /// Se le pregunta justo antes de caer. Mientras le queden fases, no cae:
        /// se transforma.
        ///
        /// Repartir las fases por tramos de vida — a los dos tercios, a un
        /// tercio — no funcionaba: el cambio pasaba en mitad de un combo y no se
        /// veia. Vaciarle la barra entera y que vuelva con ella llena se lee de
        /// golpe y se siente como haber ganado un asalto, no como un numero.
        /// </summary>
        bool ImpideLaMuerte(Damageable d)
        {
            if (_terminado || _transformando) return false;
            if (_fase >= fases) return false;

            StartCoroutine(Transformacion());
            return true;
        }

        System.Collections.IEnumerator Transformacion()
        {
            _transformando = true;
            _fase++;

            // durante la transformacion no se le puede tocar ni el pega: si no,
            // te lo llevabas por delante justo cuando cambia de forma
            float espera = duracionTransformacion + 0.35f;
            _vida.HazteInmune(espera);
            if (_cerebro != null) _cerebro.Interpreta(animTransformacion, espera);

            if (_cartel != null)
            {
                _cartel.Fase(_fase, fases);
                _cartel.Destella(new Color(0.13f, 0.83f, 0.93f), 0.75f);
            }
            PlayerHealthBar.Aviso($"FASE {_fase}", new Color(0.13f, 0.83f, 0.93f));
            Audio.Sfx.Play("chrono", 1f, 0.5f);
            Audio.Sfx.Play("detener", 0.8f, 0.7f);

            if (_musica != null) _musica.Fase(_fase);
            if (_escenario != null) _escenario.SetPhase(_fase);

            // la barra se vacia mientras dura la animacion
            if (_cartel != null) _cartel.Vida(0f);

            yield return new WaitForSeconds(duracionTransformacion);

            // y vuelve entero, con otro repertorio
            _vida.CuraDelTodo();
            if (_cartel != null)
            {
                _cartel.Vida(1f);
                _cartel.Destella(new Color(0.98f, 0.75f, 0.14f), 0.5f);
            }
            if (_cerebro != null)
                _cerebro.CambiaDeFase(idleSiguienteFase, ataquesSiguienteFase,
                                      cadenciaSiguienteFase, velocidadSiguienteFase);

            Audio.Sfx.Play("mana", 1f, 0.6f);
            _transformando = false;
        }

        void AlCaer(Damageable _)
        {
            _terminado = true;
            if (_cartel != null)
            {
                _cartel.Vida(0f);
                _cartel.Destella(new Color(0.98f, 0.75f, 0.14f), 0.7f);
                _cartel.Cierra();
                Destroy(_cartel.gameObject, 3f);
            }
            if (_musica != null)
            {
                _musica.Termina();
                Destroy(_musica.gameObject, 6f);
            }
            if (_ambiente != null) _ambiente.Apartate(1f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.98f, 0.75f, 0.14f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radioEntrada);
        }
    }
}
