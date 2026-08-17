using System.Collections.Generic;
using UnityEngine;

namespace Ekkar.Audio
{
    /// <summary>
    /// Efectos de sonido generados por codigo, como la musica del menu. El
    /// juego no tiene archivos de audio para saltar, pegar o usar habilidades,
    /// y esperar a tenerlos significaba jugar en silencio.
    ///
    /// Cada efecto es una onda sencilla con su envolvente: nada sofisticado,
    /// pero da el golpe seco que hace falta para que una accion se sienta.
    /// </summary>
    public static class Sfx
    {
        const int Hz = 44100;
        static readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        static AudioSource _fuente;

        public static float Volumen = 0.75f;   // los golpes se oyen sin tapar nada

        static AudioSource Fuente()
        {
            if (_fuente != null) return _fuente;
            var go = new GameObject("_Sfx");
            Object.DontDestroyOnLoad(go);
            _fuente = go.AddComponent<AudioSource>();
            _fuente.playOnAwake = false;
            _fuente.spatialBlend = 0f;          // se oye igual de cerca o de lejos
            return _fuente;
        }

        public static void Play(string nombre, float volumen = 1f, float tono = 1f)
        {
            var clip = Clip(nombre);
            if (clip == null) return;
            var f = Fuente();
            f.pitch = Mathf.Clamp(tono * Random.Range(0.94f, 1.06f), 0.4f, 2.5f);
            f.PlayOneShot(clip, Mathf.Clamp01(volumen * Volumen));
        }

        /// <summary>Reproduce un clip de verdad por el mismo canal 2D.</summary>
        public static void Reproducir(AudioClip clip, float volumen = 1f)
        {
            if (clip == null) return;
            var f = Fuente();
            f.pitch = 1f;
            f.PlayOneShot(clip, Mathf.Clamp01(volumen * Volumen));
        }

        // ------------------------------------------------- recorte de silencio

        static readonly Dictionary<AudioClip, float> _arranques = new Dictionary<AudioClip, float>();
        static AudioSource[] _canales;
        static int _siguiente;

        /// <summary>
        /// Igual que <see cref="Reproducir"/>, pero saltandose el silencio del
        /// principio del clip.
        ///
        /// Los sonidos de los enemigos vienen recortados de los videos de origen
        /// y algunos arrancan hasta tres decimas tarde. Sumando el retardo del
        /// propio golpe, el quejido llegaba casi medio segundo despues de que
        /// vieras el impacto, y se oia como si el sonido fuera por detras de la
        /// accion. Aqui se busca la primera muestra que suena de verdad y se
        /// empieza a reproducir justo ahi.
        /// </summary>
        public static void ReproducirDesdeElSonido(AudioClip clip, float volumen = 1f, float tono = 1f)
        {
            if (clip == null) return;

            float desde = Arranque(clip);
            if (desde <= 0.001f) { Reproducir(clip, volumen); return; }

            var canal = Canal();
            if (canal == null) { Reproducir(clip, volumen); return; }

            canal.clip = clip;
            canal.pitch = Mathf.Clamp(tono, 0.4f, 2.5f);
            canal.volume = Mathf.Clamp01(volumen * Volumen);
            canal.time = Mathf.Min(desde, Mathf.Max(0f, clip.length - 0.05f));
            canal.Play();
        }

        /// <summary>Segundo en el que el clip empieza a sonar de verdad.</summary>
        static float Arranque(AudioClip clip)
        {
            if (_arranques.TryGetValue(clip, out float guardado)) return guardado;

            float resultado = 0f;
            try
            {
                // con mirar el primer medio segundo sobra: si el sonido empieza
                // mas tarde que eso, el problema no es el silencio
                int muestras = Mathf.Min(clip.samples, Mathf.CeilToInt(clip.frequency * 0.5f));
                var datos = new float[muestras * clip.channels];
                if (clip.GetData(datos, 0))
                {
                    const float umbral = 0.03f;
                    for (int i = 0; i < datos.Length; i++)
                    {
                        if (Mathf.Abs(datos[i]) < umbral) continue;
                        // un pelin antes, para no comerse el ataque del sonido
                        resultado = Mathf.Max(0f, (i / clip.channels) / (float)clip.frequency - 0.01f);
                        break;
                    }
                }
            }
            catch
            {
                // clips en streaming o sin lectura: se reproducen enteros
                resultado = 0f;
            }

            _arranques[clip] = resultado;
            return resultado;
        }

        /// <summary>
        /// Cuatro canales que se van turnando. Hace falta un AudioSource propio
        /// porque PlayOneShot no admite empezar el clip por la mitad.
        /// </summary>
        static AudioSource Canal()
        {
            if (_canales == null || _canales.Length == 0 || _canales[0] == null)
            {
                var padre = Fuente().gameObject;
                _canales = new AudioSource[4];
                for (int i = 0; i < _canales.Length; i++)
                {
                    var a = padre.AddComponent<AudioSource>();
                    a.playOnAwake = false;
                    a.spatialBlend = 0f;
                    _canales[i] = a;
                }
                _siguiente = 0;
            }

            // el que lleve mas tiempo libre; si van todos, se pisa el mas viejo
            for (int i = 0; i < _canales.Length; i++)
            {
                var a = _canales[(_siguiente + i) % _canales.Length];
                if (a != null && !a.isPlaying) { _siguiente = (_siguiente + i + 1) % _canales.Length; return a; }
            }
            var viejo = _canales[_siguiente];
            _siguiente = (_siguiente + 1) % _canales.Length;
            return viejo;
        }

        static AudioClip Clip(string nombre)
        {
            if (_cache.TryGetValue(nombre, out var hecho)) return hecho;

            // Un archivo de verdad manda sobre lo generado. Basta con dejar el
            // wav u ogg en Assets/Resources/Sfx/ con el nombre del efecto:
            // no hay que tocar ni cablear nada, se coge solo al arrancar.
            var real = Resources.Load<AudioClip>("Sfx/" + nombre);
            if (real != null)
            {
                _cache[nombre] = real;
                return real;
            }

            AudioClip clip = nombre switch
            {
                // salto: impulso corto hacia arriba con un soplo de aire debajo
                "salto"     => Mezcla(Tono(0.20f, 260f, 700f, 0.45f, Onda.Cuadrada),
                                      Ruido(0.16f, 0.30f, 1200f)),
                // aterrizar: golpe grave con polvo
                "aterrizar" => Mezcla(Tono(0.20f, 150f, 55f, 0.65f, Onda.Seno),
                                      Ruido(0.14f, 0.35f, 700f)),
                "paso"      => Ruido(0.07f, 0.22f, 900f),
                // espada: el filo cortando el aire, con silbido agudo
                "espada"    => Mezcla(Ruido(0.16f, 0.85f, 2600f),
                                      Tono(0.12f, 1400f, 500f, 0.25f, Onda.Seno)),
                // impacto: madera y hueso, grave y seco
                "impacto"   => Mezcla(Tono(0.20f, 200f, 55f, 0.9f, Onda.Sierra),
                                      Ruido(0.12f, 0.6f, 500f)),
                "dano"      => Mezcla(Tono(0.26f, 460f, 80f, 0.8f, Onda.Cuadrada),
                                      Ruido(0.14f, 0.45f, 800f)),
                "muerte"    => Mezcla(Tono(0.70f, 320f, 35f, 0.7f, Onda.Sierra),
                                      Ruido(0.45f, 0.35f, 400f)),
                // dash: aire desplazado, con un pico al arrancar
                "dash"      => Mezcla(Ruido(0.26f, 0.7f, 1400f),
                                      Tono(0.10f, 700f, 200f, 0.3f, Onda.Seno)),
                // detener el tiempo: un descenso largo que se queda colgado
                "detener"   => Mezcla(Tono(0.90f, 1100f, 140f, 0.55f, Onda.Seno),
                                      Tono(0.90f, 550f, 70f, 0.35f, Onda.Seno)),
                // chronobreak: cristal rompiendose hacia arriba
                "chrono"    => Mezcla(Tono(1.20f, 90f, 1100f, 0.95f, Onda.Sierra),
                                      Ruido(0.80f, 0.55f, 2200f)),
                "espada_out"=> Mezcla(Ruido(0.24f, 0.55f, 3000f),
                                      Tono(0.18f, 2200f, 900f, 0.22f, Onda.Seno)),
                // mana: un brillo que sube, para que se note el golpe cargado
                "mana"      => Mezcla(Tono(0.35f, 500f, 1400f, 0.5f, Onda.Seno),
                                      Tono(0.35f, 750f, 2100f, 0.25f, Onda.Seno)),
                _ => null,
            };

            _cache[nombre] = clip;
            return clip;
        }

        /// <summary>Suma dos capas: el cuerpo grave y el brillo agudo.</summary>
        static AudioClip Mezcla(AudioClip a, AudioClip b)
        {
            int n = Mathf.Max(a.samples, b.samples);
            var da = new float[a.samples]; a.GetData(da, 0);
            var db = new float[b.samples]; b.GetData(db, 0);
            var salida = new float[n];
            for (int i = 0; i < n; i++)
            {
                float v = (i < da.Length ? da[i] : 0f) + (i < db.Length ? db[i] : 0f);
                salida[i] = Mathf.Clamp(v, -1f, 1f);
            }
            return Crear(salida);
        }

        enum Onda { Seno, Cuadrada, Sierra }

        /// <summary>Un tono que barre de una frecuencia a otra y se apaga.</summary>
        static AudioClip Tono(float dur, float f0, float f1, float vol, Onda onda)
        {
            int n = Mathf.Max(16, (int)(Hz * dur));
            var datos = new float[n];
            float fase = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, t);
                fase += f / Hz;
                float s = onda switch
                {
                    Onda.Cuadrada => Mathf.Sign(Mathf.Sin(fase * Mathf.PI * 2f)),
                    Onda.Sierra => (fase % 1f) * 2f - 1f,
                    _ => Mathf.Sin(fase * Mathf.PI * 2f),
                };
                // ataque rapido y caida exponencial: suena a golpe, no a pitido
                float env = Mathf.Min(1f, t * 40f) * Mathf.Exp(-4.5f * t);
                datos[i] = s * env * vol;
            }
            return Crear(datos);
        }

        /// <summary>Ruido filtrado: sirve para roces, aire y metal.</summary>
        static AudioClip Ruido(float dur, float vol, float brillo = 400f)
        {
            int n = Mathf.Max(16, (int)(Hz * dur));
            var datos = new float[n];
            float anterior = 0f;
            float k = Mathf.Clamp01(brillo / 4000f);

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float r = Random.Range(-1f, 1f);
                anterior = Mathf.Lerp(anterior, r, k);
                float env = Mathf.Min(1f, t * 60f) * Mathf.Exp(-6f * t);
                datos[i] = anterior * env * vol;
            }
            return Crear(datos);
        }

        static AudioClip Crear(float[] datos)
        {
            var clip = AudioClip.Create("sfx", datos.Length, 1, Hz, false);
            clip.SetData(datos, 0);
            return clip;
        }
    }
}
