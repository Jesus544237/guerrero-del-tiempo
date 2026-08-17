using Ekkar.Gameplay;
using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>
    /// El tornado de rayos del salto doble.
    ///
    /// Al segundo salto Ekkar arrastra consigo un remolino de descargas que
    /// gira a su alrededor y muerde a todo lo que tenga cerca. Es lo que
    /// convierte el salto doble en una jugada y no solo en una forma de llegar
    /// mas alto: te lanzas al aire en medio de un grupo y sales limpiando.
    ///
    /// Los rayos son sprites dibujados por codigo, como el resto de los
    /// efectos del juego, para no depender de arte que no existe todavia. Van
    /// en dos anillos que giran en sentidos contrarios: uno solo se lee como
    /// una rueda, dos se leen como una tormenta.
    /// </summary>
    public class StormBurst : MonoBehaviour
    {
        static readonly Color Rayo = new Color(0.60f, 0.94f, 1f);
        static readonly Color Oro = new Color(0.98f, 0.80f, 0.30f);

        Transform _sigue;
        SpriteRenderer[] _rayos;
        SpriteRenderer _nucleo;
        float[] _angulos;
        int[] _sentidos;

        float _duracion, _vivo;
        float _radio;
        int _dano;
        float _proximoTic;
        int _tics;

        /// <summary>Levanta la tormenta encima de quien la invoca.</summary>
        public static StormBurst Lanza(Transform sigue, float radio, int dano,
                                       float duracion = 1.1f, int tics = 3)
        {
            if (sigue == null) return null;

            var go = new GameObject("TormentaDeRayos");
            var t = go.AddComponent<StormBurst>();
            t._sigue = sigue;
            t._radio = radio;
            t._dano = dano;
            t._duracion = duracion;
            t._tics = Mathf.Max(1, tics);
            t.Construye();
            return t;
        }

        void Construye()
        {
            transform.position = Centro();

            // el nucleo: un fogonazo redondo que se abre y se apaga
            _nucleo = Pieza("Nucleo", Halo(), Rayo, 60);
            _nucleo.transform.localScale = Vector3.one * _radio * 0.6f;

            const int cuantos = 10;
            _rayos = new SpriteRenderer[cuantos];
            _angulos = new float[cuantos];
            _sentidos = new int[cuantos];

            var bolt = Descarga();
            for (int i = 0; i < cuantos; i++)
            {
                bool fuera = i % 2 == 0;
                _rayos[i] = Pieza($"Rayo_{i:00}", bolt, fuera ? Rayo : Oro, 61);
                _angulos[i] = i * (360f / cuantos) + (fuera ? 0f : 18f);
                _sentidos[i] = fuera ? 1 : -1;         // dos anillos en contra
            }

            Audio.Sfx.Play("chrono", 0.55f, 1.5f);
            Audio.Sfx.Play("detener", 0.4f, 1.8f);
        }

        SpriteRenderer Pieza(string nombre, Sprite sp, Color c, int orden)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.color = c;
            sr.sortingOrder = orden;
            Core.SpriteMat.Aplica(sr);
            return sr;
        }

        void Update()
        {
            if (_sigue == null) { Destroy(gameObject); return; }

            _vivo += Time.deltaTime;
            float k = Mathf.Clamp01(_vivo / _duracion);

            transform.position = Centro();

            // abre rapido y se cierra despacio
            float apertura = k < 0.22f ? Ease01(k / 0.22f) : 1f;
            float apagado = k > 0.7f ? 1f - (k - 0.7f) / 0.3f : 1f;
            float radio = _radio * apertura;

            for (int i = 0; i < _rayos.Length; i++)
            {
                _angulos[i] += _sentidos[i] * (520f + i * 11f) * Time.deltaTime;
                float rad = _angulos[i] * Mathf.Deg2Rad;

                // los dos anillos van a distinta distancia
                float r = radio * (_sentidos[i] > 0 ? 0.95f : 0.62f);
                var p = new Vector3(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r * 0.62f, 0f);

                var sr = _rayos[i];
                sr.transform.localPosition = p;
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, _angulos[i] + 90f);
                sr.transform.localScale = new Vector3(1f, 0.8f + Mathf.Abs(Mathf.Sin(rad * 2f)) * 0.5f, 1f);

                // parpadeo rapido: ninguna descarga se queda encendida
                float chispa = Mathf.Repeat(_vivo * 26f + i * 0.37f, 1f) < 0.62f ? 1f : 0.25f;
                var c = sr.color;
                c.a = chispa * apagado;
                sr.color = c;
            }

            if (_nucleo != null)
            {
                _nucleo.transform.localScale = Vector3.one * radio * 1.5f;
                var c = _nucleo.color;
                c.a = (k < 0.15f ? 0.55f * (1f - k / 0.15f) : 0f) + 0.10f * apagado;
                _nucleo.color = c;
            }

            // ---- el dano, repartido en varios mordiscos
            if (_vivo >= _proximoTic && _tics > 0)
            {
                _tics--;
                _proximoTic = _vivo + _duracion / (_tics + 1f);
                Damageable.Golpear(transform.position, _radio, Damageable.Bando.Jugador, _dano);
            }

            if (k >= 1f) Destroy(gameObject);
        }

        Vector3 Centro() => _sigue.position + Vector3.up * 1.05f;

        static float Ease01(float t) => 1f - (1f - t) * (1f - t);

        // ------------------------------------------------------------ dibujo

        static Sprite _descarga, _halo;

        /// <summary>Una descarga en zigzag, 7 x 34 pixeles.</summary>
        static Sprite Descarga()
        {
            if (_descarga != null) return _descarga;

            const int w = 7, h = 34;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var vacio = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, vacio);

            // el zigzag: se va cruzando de lado a lado segun sube
            int[] centro = { 3, 3, 2, 2, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 4, 4, 3 };
            for (int y = 0; y < h; y++)
            {
                int c = centro[Mathf.Min(centro.Length - 1, y * centro.Length / h)];
                float f = 1f - Mathf.Abs(y - h * 0.5f) / (h * 0.6f);    // mas fino en las puntas
                tex.SetPixel(c, y, Color.white);
                if (f > 0.35f && c - 1 >= 0) tex.SetPixel(c - 1, y, new Color(1f, 1f, 1f, 0.75f));
                if (f > 0.55f && c + 1 < w) tex.SetPixel(c + 1, y, new Color(1f, 1f, 1f, 0.45f));
            }
            tex.Apply();

            _descarga = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 26f);
            return _descarga;
        }

        /// <summary>Un halo redondo que se desvanece hacia fuera.</summary>
        static Sprite Halo()
        {
            if (_halo != null) return _halo;

            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * 0.9f));
                }
            tex.Apply();

            _halo = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return _halo;
        }
    }
}
