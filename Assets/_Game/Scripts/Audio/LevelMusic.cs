using Ekkar.Core;
using UnityEngine;

namespace Ekkar.Audio
{
    /// <summary>
    /// Musica de fondo de cada nivel, generada por codigo igual que la del
    /// menu, de modo que el juego no depende de archivos de audio externos.
    /// Cada era tiene su color: la Edad Media es un coro grave con campana,
    /// la industrial marca un pulso de maquina, el futuro es un arpegio
    /// sintetico y la Hora Cero casi no tiene pulso.
    ///
    /// Si se asigna un clip en <see cref="musicOverride"/> se usa ese en su
    /// lugar y no se genera nada.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class LevelMusic : MonoBehaviour
    {
        public enum Era { Medieval, Industrial, Futuro, HoraCero }

        [SerializeField] Era era = Era.Medieval;
        [SerializeField] AudioClip musicOverride;
        [SerializeField, Range(0f, 1f)] float mix = 0.32f;   // ambiente por debajo de la accion
        [SerializeField] float loopSeconds = 24f;

        const int SR = 44100;
        AudioSource _source;

        // cuando entra el tema del jefe, el ambiente de la era se aparta
        float _duck = 1f, _duckObjetivo = 1f;

        void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.clip = musicOverride != null ? musicOverride : Build();

            if (!GameSettings.Loaded) GameSettings.Load();
            GameSettings.AudioChanged += ApplyVolume;
            ApplyVolume();
        }

        void OnDestroy() => GameSettings.AudioChanged -= ApplyVolume;

        void Start()
        {
            if (_source.clip != null) _source.Play();
        }

        void Update()
        {
            if (Mathf.Approximately(_duck, _duckObjetivo)) return;
            _duck = Mathf.MoveTowards(_duck, _duckObjetivo, Time.unscaledDeltaTime * 0.6f);
            ApplyVolume();
        }

        /// <summary>
        /// Aparta el ambiente de la era para dejar sitio a otra cosa. 1 es el
        /// volumen normal y 0 es callarse del todo.
        /// </summary>
        public void Apartate(float factor) => _duckObjetivo = Mathf.Clamp01(factor);

        void ApplyVolume()
        {
            if (_source != null) _source.volume = GameSettings.MusicVolume * mix * _duck;
        }

        // ------------------------------------------------------------ sintesis

        AudioClip Build()
        {
            int count = Mathf.RoundToInt(loopSeconds * SR);
            var data = new float[count];
            var rng = new System.Random((int)era * 7919 + 17);

            switch (era)
            {
                case Era.Medieval:   Medieval(data, rng);   break;
                case Era.Industrial: Industrial(data, rng); break;
                case Era.Futuro:     Futuro(data, rng);     break;
                default:             HoraCero(data, rng);   break;
            }

            SeamlessLoop(data);
            Normalize(data, 0.5f);

            var clip = AudioClip.Create($"Musica_{era}", count, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Note(float semitonesFromA2) => 110f * Mathf.Pow(2f, semitonesFromA2 / 12f);

        /// <summary>Edad Media: cuerdas graves, quintas abiertas y una campana lejana.</summary>
        void Medieval(float[] data, System.Random rng)
        {
            float dur = data.Length / (float)SR;
            float[] drone = { Note(-12f), Note(-5f), Note(0f), Note(7f) };   // A1 D2 A2 E3
            float[] gain = { 0.34f, 0.16f, 0.22f, 0.12f };

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SR;
                float acc = 0f;
                for (int d = 0; d < drone.Length; d++)
                {
                    float lfo = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * (d + 1) / dur * t);
                    acc += Mathf.Sin(2f * Mathf.PI * drone[d] * t) * gain[d] * lfo;
                }
                data[i] = acc;
            }

            // campana: una nota cada 6 s, con armonicos y caida larga
            float[] bells = { Note(12f), Note(15f), Note(19f), Note(12f) };
            AddHits(data, bells, dur / bells.Length, 0.10f, 1.6f, harmonics: true);
        }

        /// <summary>Industrial: pulso de maquina, vapor y un yunque metalico.</summary>
        void Industrial(float[] data, System.Random rng)
        {
            float dur = data.Length / (float)SR;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SR;
                float bass = Mathf.Sin(2f * Mathf.PI * Note(-12f) * t) * 0.30f;
                float fifth = Mathf.Sin(2f * Mathf.PI * Note(-5f) * t) * 0.14f;

                // pulso de piston cada medio segundo
                float beat = Mathf.Repeat(t, 0.5f);
                float thump = Mathf.Exp(-18f * beat) * Mathf.Sin(2f * Mathf.PI * 55f * beat) * 0.5f;

                data[i] = bass + fifth + thump;
            }

            // vapor: ruido filtrado que sube y baja
            float lp = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SR;
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (n - lp) * 0.02f;
                data[i] += lp * 0.22f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2f / dur * t));
            }

            AddHits(data, new[] { Note(24f), Note(19f) }, dur / 2f, 0.07f, 0.5f, harmonics: true);
        }

        /// <summary>Futuro: arpegio sintetico y colchon frio.</summary>
        void Futuro(float[] data, System.Random rng)
        {
            float dur = data.Length / (float)SR;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SR;
                float pad = Mathf.Sin(2f * Mathf.PI * Note(0f) * t) * 0.18f
                          + Mathf.Sin(2f * Mathf.PI * Note(3f) * t) * 0.12f
                          + Mathf.Sin(2f * Mathf.PI * Note(7f) * t) * 0.10f;
                data[i] = pad;
            }

            // arpegio rapido de onda cuadrada suave
            float step = 0.25f;
            float[] seq = { Note(12f), Note(15f), Note(19f), Note(22f), Note(19f), Note(15f) };
            int notes = Mathf.FloorToInt(dur / step);
            for (int n = 0; n < notes; n++)
            {
                int start = Mathf.RoundToInt(n * step * SR);
                int len = Mathf.RoundToInt(step * 0.9f * SR);
                float f = seq[n % seq.Length];
                for (int i = 0; i < len && start + i < data.Length; i++)
                {
                    float tt = i / (float)SR;
                    float env = Mathf.Exp(-7f * tt) * Mathf.Clamp01(i / (SR * 0.004f));
                    float sq = Mathf.Sin(2f * Mathf.PI * f * tt) >= 0f ? 1f : -1f;
                    data[start + i] += sq * env * 0.07f;
                }
            }
        }

        /// <summary>Hora Cero: casi sin pulso, solo aire y un latido lentisimo.</summary>
        void HoraCero(float[] data, System.Random rng)
        {
            float dur = data.Length / (float)SR;
            float lp = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SR;
                float sub = Mathf.Sin(2f * Mathf.PI * Note(-24f) * t) * 0.32f;
                float shimmer = Mathf.Sin(2f * Mathf.PI * Note(19f) * t) * 0.05f
                              * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 3f / dur * t));
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (n - lp) * 0.004f;
                data[i] = sub + shimmer + lp * 0.30f;
            }
            AddHits(data, new[] { Note(0f) }, dur / 2f, 0.08f, 3.2f, harmonics: false);
        }

        void AddHits(float[] data, float[] freqs, float spacing, float gain, float decay, bool harmonics)
        {
            for (int n = 0; n * spacing * SR < data.Length; n++)
            {
                int start = Mathf.RoundToInt(n * spacing * SR);
                float f = freqs[n % freqs.Length];
                int len = Mathf.RoundToInt(spacing * 0.95f * SR);
                for (int i = 0; i < len && start + i < data.Length; i++)
                {
                    float t = i / (float)SR;
                    float env = Mathf.Exp(-decay * t) * Mathf.Clamp01(i / (SR * 0.006f));
                    float v = Mathf.Sin(2f * Mathf.PI * f * t);
                    if (harmonics) v += Mathf.Sin(2f * Mathf.PI * f * 2.01f * t) * 0.3f;
                    data[start + i] += v * env * gain;
                }
            }
        }

        static void SeamlessLoop(float[] data)
        {
            int fade = Mathf.Min(data.Length / 4, Mathf.RoundToInt(0.12f * SR));
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] *= k;
                data[data.Length - 1 - i] *= k;
            }
        }

        static void Normalize(float[] data, float target)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            if (peak < 0.0001f) return;
            float k = target / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }
    }
}
