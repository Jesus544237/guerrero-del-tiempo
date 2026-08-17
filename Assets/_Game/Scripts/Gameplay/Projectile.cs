using UnityEngine;

namespace Ekkar.Gameplay
{
    /// <summary>
    /// El proyectil de los enemigos que disparan de lejos: la saeta del
    /// ballestero. Vuela recto, pega al primero del bando contrario que toca y
    /// se apaga; si no toca a nadie, se apaga sola al cabo de unos segundos
    /// para no dejar basura volando por el nivel.
    ///
    /// Se dibuja por codigo (una saeta de dos tonos) para no depender de arte
    /// que todavia no existe. El dia que haya sprite de flecha, se le pasa y
    /// listo.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        float _velocidad, _muereEn, _radio;
        int _dano;
        Vector2 _direccion;
        Damageable.Bando _deQuien;

        static Sprite _saeta;

        public static Projectile Lanzar(Vector3 origen, Vector2 direccion, Damageable.Bando deQuien,
                                        int dano, float velocidad = 11f, float vida = 3f,
                                        Sprite sprite = null, Color? color = null)
        {
            var go = new GameObject("Saeta");
            go.transform.position = origen;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : Saeta();
            sr.color = color ?? new Color(0.13f, 0.83f, 0.93f);
            sr.sortingOrder = 40;
            // sin esto la saeta es invisible: el renderer 2D de URP le pone
            // material iluminado y aqui no hay luces 2D que la alumbren
            Core.SpriteMat.Aplica(sr);

            var p = go.AddComponent<Projectile>();
            p._direccion = direccion.normalized;
            p._velocidad = velocidad;
            p._dano = dano;
            p._deQuien = deQuien;
            p._muereEn = Time.time + vida;
            p._radio = 0.35f;

            // se orienta hacia donde va
            float ang = Mathf.Atan2(p._direccion.y, p._direccion.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            return p;
        }

        /// <summary>Una saeta sencilla dibujada a mano, 16x5 pixeles.</summary>
        static Sprite Saeta()
        {
            if (_saeta != null) return _saeta;

            const int w = 16, h = 5;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var vacio = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, vacio);

            // asta
            for (int x = 2; x < 12; x++) tex.SetPixel(x, 2, Color.white);
            // punta
            tex.SetPixel(12, 2, Color.white); tex.SetPixel(13, 2, Color.white);
            tex.SetPixel(12, 1, Color.white); tex.SetPixel(12, 3, Color.white);
            tex.SetPixel(14, 2, Color.white);
            // plumas
            tex.SetPixel(1, 1, Color.white); tex.SetPixel(1, 3, Color.white);
            tex.SetPixel(0, 0, Color.white); tex.SetPixel(0, 4, Color.white);
            tex.Apply();

            _saeta = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 20f);
            return _saeta;
        }

        void Update()
        {
            // con el tiempo parado la saeta se queda colgada en el aire, que es
            // media gracia de la habilidad: la ves venir y te apartas andando
            if (Core.TimeControl.Detenido)
            {
                _muereEn += Time.deltaTime;
                return;
            }

            if (Time.time >= _muereEn) { Destroy(gameObject); return; }

            transform.position += (Vector3)(_direccion * _velocidad * Time.deltaTime);

            foreach (var col in Physics2D.OverlapCircleAll(transform.position, _radio))
            {
                var d = col.GetComponentInParent<Damageable>();
                if (d == null || d.Muerto || d.Lado == _deQuien) continue;
                d.RecibirGolpe(_dano, transform.position);
                Destroy(gameObject);
                return;
            }
        }
    }
}
