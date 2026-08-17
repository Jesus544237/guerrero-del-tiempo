using System;
using System.Collections.Generic;
using Ekkar.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.UI
{
    /// <summary>
    /// Los iconos de las habilidades, abajo a la izquierda.
    ///
    /// Hasta ahora no habia forma de saber si el dash estaba listo, cuanto
    /// costaba detener el tiempo o por que el chronobreak no salia. Cada casilla
    /// dice las tres cosas de un vistazo: la tecla, lo que cuesta y cuanto le
    /// queda de enfriamiento, con el barrido oscuro girando encima.
    ///
    /// Los iconos se dibujan pixel a pixel por codigo. Es feo de escribir pero
    /// encaja con el resto del juego, que tampoco depende de arte externo, y
    /// asi no hay que importar nada para que esto se vea.
    /// </summary>
    public class AbilityBar : MonoBehaviour
    {
        [SerializeField] internal TMP_FontAsset fuente;
        [SerializeField] Vector2 margen = new Vector2(40f, 40f);
        [SerializeField] float lado = 84f;
        [SerializeField] float separacion = 12f;

        static readonly Color Oro = new Color(0.980f, 0.753f, 0.141f);
        static readonly Color Cian = new Color(0.133f, 0.827f, 0.929f);
        static readonly Color Hueso = new Color(0.886f, 0.906f, 0.922f);
        static readonly Color Apagado = new Color(0.36f, 0.30f, 0.52f);
        static readonly Color Fondo = new Color(0.06f, 0.03f, 0.12f, 0.92f);
        static readonly Color Rojo = new Color(0.90f, 0.25f, 0.25f);

        class Casilla
        {
            public Image marco, icono, barrido, destello;
            public TextMeshProUGUI tecla, coste;
            public RectTransform raiz;
            public Func<float> restante;
            public Func<float> total;
            public int mana;
            public bool estabaLista;
            public float golpe;      // animacion de "ya esta lista"
        }

        readonly List<Casilla> _casillas = new List<Casilla>();
        EkkarController _ekkar;
        PlayerCombat _combate;
        TextMeshProUGUI _aviso;
        GameObject _raiz;
        float _avisoHasta;

        void Start() => Engancha();

        void Engancha()
        {
            _ekkar = FindAnyObjectByType<EkkarController>();
            if (_ekkar == null) return;
            _combate = _ekkar.GetComponent<PlayerCombat>();

            Construye();

            if (_combate != null) _combate.SinMana += AvisaSinMana;
        }

        void OnDestroy()
        {
            if (_combate != null) _combate.SinMana -= AvisaSinMana;
            if (_raiz != null) Destroy(_raiz);
        }

        void AvisaSinMana()
        {
            _avisoHasta = Time.unscaledTime + 1.1f;
            Audio.Sfx.Play("espada_out", 0.35f, 0.55f);
        }

        void Update()
        {
            if (_ekkar == null) { Engancha(); return; }
            if (_casillas.Count == 0) return;

            int mana = _combate != null ? _combate.Mana : 0;
            float dt = Time.unscaledDeltaTime;

            foreach (var c in _casillas)
            {
                float queda = c.restante();
                float total = Mathf.Max(0.0001f, c.total());
                bool enfriando = queda > 0.01f;
                bool sinMana = mana < c.mana;
                bool lista = !enfriando && !sinMana;

                // barrido: el trozo oscuro que va desapareciendo en sentido horario
                c.barrido.fillAmount = enfriando ? Mathf.Clamp01(queda / total) : 0f;

                Color marco = enfriando ? Apagado : (sinMana ? Cian * 0.55f : Oro);
                if (lista)
                {
                    // late despacio para que se vea que esta disponible
                    float latido = 0.82f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f)) * 0.18f;
                    marco = Oro * latido;
                    marco.a = 1f;
                }
                c.marco.color = marco;

                c.icono.color = enfriando ? new Color(1f, 1f, 1f, 0.32f)
                              : sinMana ? new Color(1f, 1f, 1f, 0.5f)
                              : Color.white;
                c.coste.color = sinMana ? Rojo : Cian;
                c.tecla.color = lista ? Hueso : Hueso * 0.6f;

                // chispazo al terminar el enfriamiento
                if (lista && !c.estabaLista) c.golpe = 1f;
                c.estabaLista = lista;

                if (c.golpe > 0f)
                {
                    c.golpe = Mathf.Max(0f, c.golpe - dt * 3.2f);
                    c.destello.color = new Color(1f, 1f, 1f, c.golpe * 0.55f);
                    c.raiz.localScale = Vector3.one * (1f + c.golpe * 0.14f);
                }
                else
                {
                    c.destello.color = new Color(1f, 1f, 1f, 0f);
                    c.raiz.localScale = Vector3.one;
                }
            }

            if (_aviso != null)
            {
                float resta = _avisoHasta - Time.unscaledTime;
                bool on = resta > 0f;
                if (_aviso.gameObject.activeSelf != on) _aviso.gameObject.SetActive(on);
                if (on) _aviso.color = new Color(Rojo.r, Rojo.g, Rojo.b, Mathf.Clamp01(resta / 0.4f));
            }
        }

        // ---------------------------------------------------------- montaje

        void Construye()
        {
            // suelto en la escena: si cuelga del 10_HUD queda dentro del Canvas
            // de la barra de vida, y un Canvas anidado ni se estira a la
            // pantalla ni respeta el CanvasScaler
            var raiz = new GameObject("UI_Habilidades", typeof(RectTransform));
            _raiz = raiz;

            var canvas = raiz.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 310;

            var escala = raiz.AddComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920f, 1080f);
            escala.matchWidthOrHeight = 0.5f;

            var fila = new GameObject("Fila", typeof(RectTransform));
            fila.transform.SetParent(raiz.transform, false);
            var frt = (RectTransform)fila.transform;
            frt.anchorMin = frt.anchorMax = new Vector2(0f, 0f);
            frt.pivot = new Vector2(0f, 0f);
            frt.anchoredPosition = margen;
            frt.sizeDelta = new Vector2(lado * 4f + separacion * 3f, lado + 26f);

            float x = 0f;
            x = Nueva(frt, x, "SHIFT", "DASH", Icono.Dash, _ekkar.ManaDash,
                      () => _ekkar.DashRestante, () => _ekkar.DashCooldown);
            x = Nueva(frt, x, "E", "TIEMPO", Icono.Reloj, _ekkar.ManaDetener,
                      () => _ekkar.DetenerRestante, () => _ekkar.DetenerCooldown);
            x = Nueva(frt, x, "R", "CHRONO", Icono.Rotura, _ekkar.ManaChrono,
                      () => _ekkar.ChronoRestante, () => _ekkar.ChronoCooldown);
            Nueva(frt, x, "K", "CARGADO", Icono.Espada,
                  _combate != null ? _combate.CosteCargado : 4,
                  () => 0f, () => 1f);

            _aviso = new GameObject("SinMana", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            _aviso.transform.SetParent(frt, false);
            if (fuente != null) _aviso.font = fuente;
            _aviso.text = "SIN MANA";
            _aviso.fontSize = 26f;
            _aviso.color = Rojo;
            _aviso.alignment = TextAlignmentOptions.Left;
            _aviso.raycastTarget = false;
            var art = _aviso.rectTransform;
            art.anchorMin = art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 0f);
            art.sizeDelta = new Vector2(360f, 32f);
            art.anchoredPosition = new Vector2(2f, 8f);
            _aviso.gameObject.SetActive(false);
        }

        float Nueva(RectTransform fila, float x, string tecla, string nombre, Icono icono,
                    int mana, Func<float> restante, Func<float> total)
        {
            var c = new Casilla { restante = restante, total = total, mana = mana };

            var marco = Caja("Casilla_" + nombre, Oro, fila);
            c.marco = marco;
            c.raiz = marco.rectTransform;
            c.raiz.anchorMin = c.raiz.anchorMax = new Vector2(0f, 0f);
            c.raiz.pivot = new Vector2(0f, 0f);
            c.raiz.sizeDelta = new Vector2(lado, lado);
            c.raiz.anchoredPosition = new Vector2(x, 26f);

            var fondo = Caja("Fondo", Fondo, marco.transform);
            Dentro(fondo.rectTransform, 3f);

            c.icono = Caja("Icono", Color.white, fondo.transform);
            c.icono.sprite = Sprites.Dame(icono);
            c.icono.type = Image.Type.Simple;
            Dentro(c.icono.rectTransform, 12f);

            // el barrido oscuro que gira: es el enfriamiento
            c.barrido = Caja("Enfriamiento", new Color(0.02f, 0.01f, 0.05f, 0.78f), fondo.transform);
            c.barrido.sprite = Sprites.Blanco();
            c.barrido.type = Image.Type.Filled;
            c.barrido.fillMethod = Image.FillMethod.Radial360;
            c.barrido.fillOrigin = (int)Image.Origin360.Top;
            c.barrido.fillClockwise = false;
            c.barrido.fillAmount = 0f;
            Dentro(c.barrido.rectTransform, 0f);

            c.destello = Caja("Destello", new Color(1f, 1f, 1f, 0f), fondo.transform);
            Dentro(c.destello.rectTransform, 0f);

            c.tecla = Texto("Tecla", tecla, 20f, Hueso, marco.transform,
                            new Vector2(0f, -14f), new Vector2(lado + 24f, 24f));
            c.tecla.rectTransform.anchorMin = c.tecla.rectTransform.anchorMax = new Vector2(0.5f, 0f);

            c.coste = Texto("Coste", mana.ToString(), 22f, Cian, fondo.transform,
                            new Vector2(-6f, 6f), new Vector2(28f, 24f));
            c.coste.alignment = TextAlignmentOptions.BottomRight;
            c.coste.rectTransform.anchorMin = c.coste.rectTransform.anchorMax = new Vector2(1f, 0f);
            c.coste.rectTransform.pivot = new Vector2(1f, 0f);

            _casillas.Add(c);
            return x + lado + separacion;
        }

        static Image Caja(string nombre, Color color, Transform padre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            img.sprite = Sprites.Blanco();
            return img;
        }

        TextMeshProUGUI Texto(string nombre, string texto, float tam, Color color,
                              Transform padre, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (fuente != null) t.font = fuente;
            t.text = texto;
            t.fontSize = tam;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return t;
        }

        static void Dentro(RectTransform rt, float margen)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margen, margen);
            rt.offsetMax = new Vector2(-margen, -margen);
        }

        // ------------------------------------------------------------ iconos

        internal enum Icono { Dash, Reloj, Rotura, Espada }

        /// <summary>
        /// Los cuatro iconos, dibujados a mano en una rejilla de 24x24 y
        /// cacheados. Punto de filtrado y nada de suavizado: tienen que
        /// cortar igual que el resto del pixel art.
        /// </summary>
        static class Sprites
        {
            const int N = 24;
            static readonly Dictionary<Icono, Sprite> _cache = new Dictionary<Icono, Sprite>();
            static Sprite _blanco;

            public static Sprite Blanco()
            {
                if (_blanco != null) return _blanco;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                _blanco = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                return _blanco;
            }

            public static Sprite Dame(Icono cual)
            {
                if (_cache.TryGetValue(cual, out var hecho) && hecho != null) return hecho;

                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color[N * N];
                for (int i = 0; i < px.Length; i++) px[i] = new Color(0f, 0f, 0f, 0f);

                switch (cual)
                {
                    case Icono.Dash:   Dash(px);   break;
                    case Icono.Reloj:  Reloj(px);  break;
                    case Icono.Rotura: Rotura(px); break;
                    default:           Espada(px); break;
                }

                tex.SetPixels(px);
                tex.Apply();
                var sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
                _cache[cual] = sp;
                return sp;
            }

            static void P(Color[] px, int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= N || y >= N) return;
                px[y * N + x] = c;
            }

            static void Linea(Color[] px, int x0, int y0, int x1, int y1, Color c)
            {
                int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
                int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
                int err = dx - dy;
                for (int guard = 0; guard < 256; guard++)
                {
                    P(px, x0, y0, c);
                    if (x0 == x1 && y0 == y1) return;
                    int e2 = err * 2;
                    if (e2 > -dy) { err -= dy; x0 += sx; }
                    if (e2 < dx) { err += dx; y0 += sy; }
                }
            }

            static void Circulo(Color[] px, int cx, int cy, int r, Color c)
            {
                for (int a = 0; a < 360; a += 4)
                {
                    float rad = a * Mathf.Deg2Rad;
                    P(px, cx + Mathf.RoundToInt(Mathf.Cos(rad) * r),
                          cy + Mathf.RoundToInt(Mathf.Sin(rad) * r), c);
                }
            }

            /// <summary>Dash: una punta de flecha con su estela.</summary>
            static void Dash(Color[] px)
            {
                var c = Color.white;
                var tenue = new Color(1f, 1f, 1f, 0.55f);

                // punta
                for (int i = 0; i < 8; i++)
                {
                    Linea(px, 12 + i, 12 - i, 12 + i, 12 + i, c);
                }
                // estela: tres rayas de distinto largo
                for (int x = 1; x <= 9; x++) P(px, x, 12, c);
                for (int x = 3; x <= 9; x++) P(px, x, 16, tenue);
                for (int x = 3; x <= 9; x++) P(px, x, 8, tenue);
            }

            /// <summary>Detener el tiempo: la esfera de un reloj con sus agujas.</summary>
            static void Reloj(Color[] px)
            {
                var c = Color.white;
                Circulo(px, 12, 12, 9, c);
                Circulo(px, 12, 12, 8, new Color(1f, 1f, 1f, 0.35f));

                // marcas de las doce, las tres, las seis y las nueve
                P(px, 12, 21, c); P(px, 12, 3, c); P(px, 21, 12, c); P(px, 3, 12, c);

                // agujas paradas en un angulo raro: el tiempo no cuadra
                Linea(px, 12, 12, 12, 18, c);
                Linea(px, 12, 12, 17, 10, c);
                P(px, 12, 12, c);
            }

            /// <summary>Chronobreak: un estallido con esquirlas.</summary>
            static void Rotura(Color[] px)
            {
                var c = Color.white;
                var tenue = new Color(1f, 1f, 1f, 0.6f);

                // rombo central
                for (int i = 0; i <= 4; i++)
                    for (int j = 0; j <= 4 - i; j++)
                    {
                        P(px, 12 + i, 12 + j, c); P(px, 12 - i, 12 + j, c);
                        P(px, 12 + i, 12 - j, c); P(px, 12 - i, 12 - j, c);
                    }

                // ocho esquirlas saliendo
                Linea(px, 12, 18, 12, 23, c);
                Linea(px, 12, 6, 12, 1, c);
                Linea(px, 18, 12, 23, 12, c);
                Linea(px, 6, 12, 1, 12, c);
                Linea(px, 16, 16, 20, 20, tenue);
                Linea(px, 8, 16, 4, 20, tenue);
                Linea(px, 16, 8, 20, 4, tenue);
                Linea(px, 8, 8, 4, 4, tenue);
            }

            /// <summary>Golpe cargado: la espada con un brillo.</summary>
            static void Espada(Color[] px)
            {
                var c = Color.white;
                var tenue = new Color(1f, 1f, 1f, 0.6f);

                // hoja
                for (int y = 8; y <= 21; y++) { P(px, 12, y, c); P(px, 13, y, c); }
                P(px, 12, 22, c); P(px, 13, 22, c); P(px, 12, 23, c);
                // guarda
                for (int x = 8; x <= 17; x++) P(px, x, 7, c);
                // empunadura
                for (int y = 2; y <= 6; y++) { P(px, 12, y, c); P(px, 13, y, c); }
                for (int x = 10; x <= 15; x++) P(px, x, 1, c);

                // chispas del mana
                P(px, 7, 18, tenue); P(px, 6, 17, tenue); P(px, 8, 17, tenue); P(px, 7, 16, tenue);
                P(px, 18, 14, tenue); P(px, 17, 13, tenue); P(px, 19, 13, tenue); P(px, 18, 12, tenue);
            }
        }
    }
}
