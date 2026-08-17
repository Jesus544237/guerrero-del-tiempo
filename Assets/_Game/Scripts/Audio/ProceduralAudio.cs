using UnityEngine;

namespace Ekkar.Audio
{
    public enum Wave { Sine, Square, Triangle, Saw, Noise }

    /// <summary>
    /// Genera los sonidos del menu por codigo (chiptune) para que el proyecto
    /// no dependa de archivos de audio externos. Si mas adelante hay clips
    /// reales, basta con asignarlos en el AudioManager y estos se ignoran.
    /// </summary>
    public static class ProceduralAudio
    {
        const int SR = 44100;

        static float Sample(Wave wave, float phase, System.Random rng)
        {
            switch (wave)
            {
                case Wave.Square:   return Mathf.Sin(phase) >= 0f ? 1f : -1f;
                case Wave.Triangle: return Mathf.PingPong(phase / Mathf.PI, 2f) - 1f;
                case Wave.Saw:      return (phase / (2f * Mathf.PI) % 1f) * 2f - 1f;
                case Wave.Noise:    return (float)(rng.NextDouble() * 2.0 - 1.0);
                default:            return Mathf.Sin(phase);
            }
        }

        /// <summary>Bip corto con barrido de frecuencia y caida exponencial.</summary>
        public static AudioClip Blip(string name, float freqFrom, float freqTo, float duration,
                                     Wave wave = Wave.Square, float volume = 0.35f, float decay = 6f)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(duration * SR));
            var data = new float[count];
            var rng = new System.Random(name.GetHashCode());

            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float freq = Mathf.Lerp(freqFrom, freqTo, t);
                phase += 2f * Mathf.PI * freq / SR;

                float attack = Mathf.Clamp01(i / (SR * 0.004f));      // 4 ms de ataque
                float env = attack * Mathf.Exp(-decay * t);
                data[i] = Sample(wave, phase, rng) * env * volume;
            }

            return FromSamples(name, data);
        }

        /// <summary>Arpegio: una secuencia de notas encadenadas.</summary>
        public static AudioClip Arpeggio(string name, float[] freqs, float noteDuration,
                                         Wave wave = Wave.Square, float volume = 0.32f, float decay = 8f)
        {
            int notes = Mathf.Max(1, freqs.Length);
            int perNote = Mathf.Max(1, Mathf.RoundToInt(noteDuration * SR));
            var data = new float[perNote * notes];
            var rng = new System.Random(name.GetHashCode());

            for (int n = 0; n < notes; n++)
            {
                float phase = 0f;
                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)perNote;
                    phase += 2f * Mathf.PI * freqs[n] / SR;
                    float attack = Mathf.Clamp01(i / (SR * 0.003f));
                    float env = attack * Mathf.Exp(-decay * t);
                    data[n * perNote + i] = Sample(wave, phase, rng) * env * volume;
                }
            }

            return FromSamples(name, data);
        }

        /// <summary>
        /// Colchon ambiental en La menor, con arpegio de campanas y un soplo de
        /// ruido. Todos los ciclos encajan en la duracion total para que el
        /// bucle sea continuo.
        /// </summary>
        public static AudioClip AmbientLoop(string name = "EkkarAmbient", float duration = 16f, float volume = 0.5f)
        {
            int count = Mathf.RoundToInt(duration * SR);
            var data = new float[count];
            var rng = new System.Random(1337);

            // --- colchon: cuatro sinusoides graves con LFO de periodo divisor
            float[] padFreq = { 55.00f, 82.41f, 110.00f, 164.81f }; // A1 E2 A2 E3
            float[] padLfo  = { 1f / 16f, 1f / 8f, 3f / 16f, 1f / 4f };
            float[] padGain = { 0.30f, 0.20f, 0.16f, 0.10f };

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SR;
                float acc = 0f;
                for (int p = 0; p < padFreq.Length; p++)
                {
                    float lfo = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * padLfo[p] * t);
                    acc += Mathf.Sin(2f * Mathf.PI * padFreq[p] * t) * padGain[p] * lfo;
                }
                data[i] = acc;
            }

            // --- campanas: pentatonica de La menor, una nota cada 2 segundos
            float[] bells = { 440.00f, 523.25f, 659.25f, 783.99f, 587.33f, 659.25f, 523.25f, 392.00f };
            float step = duration / bells.Length;
            for (int n = 0; n < bells.Length; n++)
            {
                int start = Mathf.RoundToInt(n * step * SR);
                int len = Mathf.RoundToInt(step * 0.95f * SR);
                for (int i = 0; i < len && start + i < count; i++)
                {
                    float t = i / (float)SR;
                    float env = Mathf.Exp(-2.6f * t) * Mathf.Clamp01(i / (SR * 0.01f));
                    float v = Mathf.Sin(2f * Mathf.PI * bells[n] * t) * 0.9f
                            + Mathf.Sin(2f * Mathf.PI * bells[n] * 2f * t) * 0.25f;
                    data[start + i] += v * env * 0.10f;
                }
            }

            // --- soplo de aire: ruido filtrado con LFO lento
            float lowpass = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SR;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lowpass += (noise - lowpass) * 0.008f;           // filtro paso bajo simple
                float lfo = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (1f / duration) * t);
                data[i] += lowpass * 0.35f * lfo;
            }

            // --- fundido de union para asegurar un bucle sin chasquidos
            int fade = Mathf.RoundToInt(0.08f * SR);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] *= k;
                data[count - 1 - i] *= k;
            }

            Normalize(data, volume);
            return FromSamples(name, data);
        }

        static void Normalize(float[] data, float target)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            if (peak <= 0.0001f) return;
            float k = target / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }

        static AudioClip FromSamples(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
