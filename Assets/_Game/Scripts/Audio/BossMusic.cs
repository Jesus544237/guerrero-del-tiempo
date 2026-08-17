using Ekkar.Core;
using UnityEngine;

namespace Ekkar.Audio
{
    /// <summary>
    /// El tema del jefe, generado por codigo como el resto de la banda sonora.
    ///
    /// Existe porque hasta ahora la pelea final no sonaba a nada: lo unico que
    /// se oia del Senor del Tiempo era su sonido de dano, asi que la "musica"
    /// aparecia al pegarle y desaparecia al dejar de pegarle. Esto arranca al
    /// entrar en combate y no para hasta que cae.
    ///
    /// La receta es la de siempre en un tema de jefe: un bajo obstinado que no
    /// suelta, una segunda menor por encima que no deja respirar, y el tictac
    /// de un reloj que se acelera con las fases.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BossMusic : MonoBehaviour
    {
        [SerializeField] AudioClip musicOverride;
        [SerializeField, Range(0f, 1f)] float mix = 0.45f;
        [SerializeField] float loopSeconds = 19.2f;
        [SerializeField] float entrada = 1.4f;      // segundos de fundido al empezar
        [SerializeField] float salida = 2.2f;

        const int SR = 44100;

        AudioSource _fuente;
        float _objetivo, _actual;
        bool _sonando;

        void Awake()
        {
            _fuente = GetComponent<AudioSource>();
            _fuente.loop = true;
            _fuente.playOnAwake = false;
            _fuente.spatialBlend = 0f;
            _fuente.volume = 0f;
            _fuente.clip = musicOverride != null ? musicOverride : Construye();

            if (!GameSettings.Loaded) GameSettings.Load();
            // sin esto, mover el volumen de musica en la pausa no se notaba
            // hasta el siguiente fundido: el tema del jefe se quedaba como
            // estaba en plena pelea, que es justo cuando se toca el ajuste
            GameSettings.AudioChanged += AplicaVolumen;
        }

        void OnDestroy() => GameSettings.AudioChanged -= AplicaVolumen;

        void AplicaVolumen()
        {
            if (_fuente != null) _fuente.volume = _actual * GameSettings.MusicVolume * mix;
        }

        void Update()
        {
            if (Mathf.Approximately(_actual, _objetivo)) return;

            float paso = Time.unscaledDeltaTime / Mathf.Max(0.05f, _objetivo > _actual ? entrada : salida);
            _actual = Mathf.MoveTowards(_actual, _objetivo, paso);
            AplicaVolumen();

            if (_actual <= 0.001f && _sonando)
            {
                _fuente.Stop();
                _sonando = false;
            }
        }

        public void Empieza()
        {
            if (_fuente.clip == null) return;
            if (!_sonando) { _fuente.Play(); _sonando = true; }
            _objetivo = 1f;
        }

        public void Termina() => _objetivo = 0f;

        /// <summary>Sube el tono un pelin en cada fase: la pelea aprieta.</summary>
        public void Fase(int fase) => _fuente.pitch = 1f + Mathf.Clamp(fase - 1, 0, 3) * 0.045f;

        // ------------------------------------------------------------ sintesis

        AudioClip Construye()
        {
            int n = Mathf.RoundToInt(loopSeconds * SR);
            var data = new float[n];
            var rng = new System.Random(4093);

            float dur = n / (float)SR;
            float negra = 0.4f;                     // 150 pulsos por minuto

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;

                // bajo obstinado: corchea seca, siempre la misma nota
                float paso = Mathf.Repeat(t, negra * 0.5f);
                float puerta = Mathf.Exp(-9f * paso);
                float bajo = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.55f * puerta;
                bajo += Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.18f * puerta;

                // la segunda menor: dos notas casi iguales que baten entre si
                float roce = (Mathf.Sin(2f * Mathf.PI * 233.08f * t) +
                              Mathf.Sin(2f * Mathf.PI * 246.94f * t)) * 0.055f
                             * (0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * t / dur * 3f));

                // colchon grave, para que no suene hueco
                float pad = Mathf.Sin(2f * Mathf.PI * 82.41f * t) * 0.10f;

                data[i] = bajo + roce + pad;
            }

            Tictac(data, negra);
            Golpes(data, negra * 8f);
            Aire(data, rng);

            Bucle(data);
            Normaliza(data, 0.62f);

            var clip = AudioClip.Create("Musica_Jefe", n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>El reloj: un clic agudo y corto en cada negra.</summary>
        static void Tictac(float[] data, float negra)
        {
            int paso = Mathf.RoundToInt(negra * SR);
            int largo = Mathf.RoundToInt(0.035f * SR);
            for (int inicio = 0; inicio < data.Length; inicio += paso)
            {
                bool fuerte = (inicio / paso) % 2 == 0;
                for (int i = 0; i < largo && inicio + i < data.Length; i++)
                {
                    float t = i / (float)SR;
                    float env = Mathf.Exp(-90f * t);
                    float f = fuerte ? 2400f : 1750f;
                    data[inicio + i] += Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.14f;
                }
            }
        }

        /// <summary>El campanazo que marca cada frase.</summary>
        static void Golpes(float[] data, float cada)
        {
            int paso = Mathf.RoundToInt(cada * SR);
            for (int inicio = 0; inicio < data.Length; inicio += paso)
            {
                int largo = Mathf.RoundToInt(cada * 0.9f * SR);
                for (int i = 0; i < largo && inicio + i < data.Length; i++)
                {
                    float t = i / (float)SR;
                    float env = Mathf.Exp(-1.9f * t) * Mathf.Clamp01(i / (SR * 0.004f));
                    float v = Mathf.Sin(2f * Mathf.PI * 110f * t)
                            + Mathf.Sin(2f * Mathf.PI * 164.81f * t) * 0.55f
                            + Mathf.Sin(2f * Mathf.PI * 220.9f * t) * 0.30f;
                    data[inicio + i] += v * env * 0.13f;
                }
            }
        }

        /// <summary>Ruido muy filtrado por debajo: llena los huecos.</summary>
        static void Aire(float[] data, System.Random rng)
        {
            float lp = 0f, dur = data.Length / (float)SR;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SR;
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (n - lp) * 0.008f;
                data[i] += lp * 0.26f * (0.45f + 0.55f * Mathf.Sin(2f * Mathf.PI * t / dur * 2f));
            }
        }

        static void Bucle(float[] data)
        {
            int fade = Mathf.Min(data.Length / 4, Mathf.RoundToInt(0.10f * SR));
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] *= k;
                data[data.Length - 1 - i] *= k;
            }
        }

        static void Normaliza(float[] data, float objetivo)
        {
            float pico = 0f;
            for (int i = 0; i < data.Length; i++) pico = Mathf.Max(pico, Mathf.Abs(data[i]));
            if (pico < 0.0001f) return;
            float k = objetivo / pico;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }
    }
}
