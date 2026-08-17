using Ekkar.Core;
using UnityEngine;

namespace Ekkar.Gameplay
{
    /// <summary>
    /// La reliquia del tiempo: un cristal que sueltan los enemigos al caer y
    /// que devuelve vida a Ekkar.
    ///
    /// Antes no habia absolutamente ninguna forma de recuperar vida en mitad de
    /// un nivel. Subir los puntos de vida solo alarga lo inevitable — si cada
    /// golpe recibido es permanente, al final del recorrido siempre llegas con
    /// lo puesto. Esto cierra el bucle: peleas bien, te curas; peleas mal, te
    /// quedas sin margen.
    ///
    /// Sale despedida hacia arriba, cae, se queda flotando y late. Si nadie la
    /// coge, parpadea los ultimos segundos y desaparece, para que no se acumulen
    /// veinte reliquias por el suelo.
    /// </summary>
    public class HealthPickup : MonoBehaviour
    {
        static readonly Color Cristal = new Color(0.35f, 0.98f, 0.62f);

        int _cura;
        float _muereEn;
        float _fase;
        SpriteRenderer _sr, _halo;
        Vector2 _velocidad;
        float _sueloY;
        bool _posado;

        /// <summary>Suelta una reliquia con un pequeno salto.</summary>
        public static HealthPickup Suelta(Vector3 donde, int cura = 2, float vida = 14f)
        {
            var go = new GameObject("ReliquiaDelTiempo");
            go.transform.position = donde;

            var p = go.AddComponent<HealthPickup>();
            p._cura = Mathf.Max(1, cura);
            p._muereEn = Time.time + vida;
            p._sueloY = donde.y;
            p._velocidad = new Vector2(Random.Range(-1.4f, 1.4f), Random.Range(4.5f, 6f));
            p.Construye();
            return p;
        }

        void Construye()
        {
            _halo = Pieza("Halo", Halo(), new Color(Cristal.r, Cristal.g, Cristal.b, 0.35f), 44);
            _halo.transform.localScale = Vector3.one * 1.5f;
            _sr = Pieza("Cristal", Cristal2D(), Cristal, 45);
        }

        SpriteRenderer Pieza(string nombre, Sprite sp, Color c, int orden)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.color = c;
            sr.sortingOrder = orden;
            SpriteMat.Aplica(sr);
            return sr;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // con el tiempo parado la reliquia tambien se queda en el aire
            if (TimeControl.Detenido) { _muereEn += dt; return; }

            _fase += dt;

            if (!_posado)
            {
                _velocidad.y -= 18f * dt;
                transform.position += (Vector3)(_velocidad * dt);
                if (transform.position.y <= _sueloY && _velocidad.y < 0f)
                {
                    _posado = true;
                    var p = transform.position;
                    p.y = _sueloY;
                    transform.position = p;
                }
            }
            else
            {
                // flota y gira despacio
                var p = transform.position;
                p.y = _sueloY + 0.45f + Mathf.Sin(_fase * 2.4f) * 0.16f;
                transform.position = p;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_fase * 1.6f) * 14f);
            }

            float queda = _muereEn - Time.time;
            if (queda <= 0f) { Destroy(gameObject); return; }

            // late siempre, y parpadea cuando se va a ir
            float latido = 0.75f + Mathf.Abs(Mathf.Sin(_fase * 3.4f)) * 0.25f;
            bool avisando = queda < 3.5f && Mathf.Repeat(queda, 0.34f) < 0.17f;
            float alfa = avisando ? 0.25f : latido;

            if (_sr != null) _sr.color = new Color(Cristal.r, Cristal.g, Cristal.b, alfa);
            if (_halo != null)
            {
                _halo.color = new Color(Cristal.r, Cristal.g, Cristal.b, alfa * 0.35f);
                _halo.transform.localScale = Vector3.one * (1.4f + Mathf.Sin(_fase * 3.4f) * 0.22f);
            }

            Recoge();
        }

        void Recoge()
        {
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, 0.85f))
            {
                var d = col.GetComponentInParent<Damageable>();
                if (d == null || d.Muerto || d.Lado != Damageable.Bando.Jugador) continue;
                if (d.Vida >= d.VidaMaxima) continue;      // lleno: se queda esperando

                int puesto = d.Curar(_cura);
                if (puesto <= 0) continue;

                Audio.Sfx.Play("mana", 0.8f, 1.35f);
                UI.PlayerHealthBar.Aviso($"+{puesto}", Cristal);
                Destroy(gameObject);
                return;
            }
        }

        // ------------------------------------------------------------ dibujo

        static Sprite _cristal, _halo2;

        /// <summary>Un cristal romboidal de 16x22, con brillo dentro.</summary>
        static Sprite Cristal2D()
        {
            if (_cristal != null) return _cristal;

            const int w = 16, h = 22;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var vacio = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, vacio);

            // rombo alargado
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Abs(x - cx) / 6.5f + Mathf.Abs(y - cy) / 10.5f;
                    if (d > 1f) continue;
                    // borde mas claro, centro mas vivo
                    tex.SetPixel(x, y, d > 0.78f ? new Color(1f, 1f, 1f, 0.95f) : Color.white);
                }

            // reflejo
            for (int y = 12; y <= 17; y++) tex.SetPixel(6, y, new Color(1f, 1f, 1f, 0.6f));
            tex.SetPixel(7, 16, new Color(1f, 1f, 1f, 0.6f));
            tex.Apply();

            _cristal = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 26f);
            return _cristal;
        }

        static Sprite Halo()
        {
            if (_halo2 != null) return _halo2;

            const int n = 40;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();

            _halo2 = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n * 0.6f);
            return _halo2;
        }
    }
}
