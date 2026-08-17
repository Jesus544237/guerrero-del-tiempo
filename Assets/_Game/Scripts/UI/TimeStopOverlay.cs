using Ekkar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.UI
{
    /// <summary>
    /// Lo que se ve cuando Ekkar detiene el tiempo.
    ///
    /// La habilidad ya funcionaba por dentro, pero no se notaba: los enemigos
    /// se quedaban quietos y no habia forma de saber si habia salido, cuanto
    /// duraba, ni siquiera si habias gastado el mana. Aqui esta la otra mitad:
    /// el fogonazo de rayo, el rotulo parpadeando y la barra que se vacia.
    ///
    /// Los tres primeros latidos son fuertes y rapidos — el golpe de "algo
    /// acaba de pasar" — y luego se queda un velo tenue del mismo color que
    /// acompana el resto de la habilidad, para que en todo momento se lea que
    /// el mundo sigue parado.
    /// </summary>
    public class TimeStopOverlay : MonoBehaviour
    {
        [SerializeField] internal TMP_FontAsset fuente;

        /// <summary>El azul electrico del rayo, el mismo de los cristales.</summary>
        static readonly Color Rayo = new Color(0.60f, 0.94f, 1f);
        static readonly Color Cian = new Color(0.13f, 0.83f, 0.93f);
        static readonly Color Oscuro = new Color(0.04f, 0.02f, 0.08f, 0.85f);

        // los latidos del fogonazo: segundo y fuerza
        static readonly (float t, float a)[] Latidos =
        {
            (0.00f, 0.62f), (0.07f, 0.05f), (0.13f, 0.45f), (0.20f, 0.05f),
            (0.27f, 0.30f), (0.34f, 0.05f), (0.44f, 0.16f), (0.56f, 0.07f),
        };

        Canvas _canvas;
        GameObject _raiz;
        CanvasGroup _grupo;
        Image _tinte, _relleno, _brilloArriba, _brilloAbajo, _placa;
        TextMeshProUGUI _rotulo, _sombra, _pie;
        RectTransform _bloque;

        float _desde = -99f;
        bool _activo;

        void Start()
        {
            Construye();
            Apaga();
            TimeControl.Empieza += AlEmpezar;
        }

        void OnDestroy()
        {
            TimeControl.Empieza -= AlEmpezar;
            if (_raiz != null) Destroy(_raiz);
        }

        void AlEmpezar(float duracion)
        {
            _desde = Time.time;
            _activo = true;
            if (_grupo != null) _grupo.alpha = 1f;
            if (_bloque != null) _bloque.localScale = Vector3.one * 1.18f;
            Audio.Sfx.Play("detener", 0.9f, 0.8f);
        }

        void Update()
        {
            if (!_activo) return;

            bool sigue = TimeControl.Detenido;
            float desdeInicio = Time.time - _desde;

            // ---- el fogonazo: se interpola entre los latidos de la tabla
            float tinte = sigue ? Fogonazo(desdeInicio) : 0f;
            if (_tinte != null) _tinte.color = new Color(Rayo.r, Rayo.g, Rayo.b, tinte);

            // ---- el rotulo parpadea fuerte al principio y luego se calma
            if (_rotulo != null)
            {
                float parpadeo = desdeInicio < 0.7f
                    ? (Mathf.Repeat(desdeInicio * 22f, 1f) < 0.55f ? 1f : 0.15f)
                    : 0.78f + Mathf.Sin(Time.time * 6f) * 0.18f;
                _rotulo.color = new Color(Rayo.r, Rayo.g, Rayo.b, parpadeo);
                // la sombra sigue al rotulo: sin ella, en el pico del fogonazo
                // el texto es del mismo color que la pantalla y desaparece
                if (_sombra != null) _sombra.color = new Color(0.02f, 0.03f, 0.09f, parpadeo * 0.95f);
            }

            // el bloque entra con un golpe de escala y se asienta
            if (_bloque != null)
                _bloque.localScale = Vector3.Lerp(_bloque.localScale, Vector3.one, Time.deltaTime * 9f);

            // ---- la barra de lo que queda
            float f = TimeControl.Fraccion;
            if (_relleno != null)
            {
                _relleno.rectTransform.localScale = new Vector3(Mathf.Clamp01(f), 1f, 1f);
                // se pone en ambar en el ultimo cuarto: se acaba
                _relleno.color = f > 0.25f ? Rayo : Color.Lerp(new Color(0.98f, 0.55f, 0.14f), Rayo, f * 4f);
            }
            if (_pie != null)
                _pie.text = $"{Mathf.Max(0f, TimeControl.Restante):0.0}s";

            float rayo = Mathf.Clamp01(tinte * 2.2f);
            if (_brilloArriba != null) _brilloArriba.color = new Color(Rayo.r, Rayo.g, Rayo.b, rayo * 0.8f);
            if (_brilloAbajo != null) _brilloAbajo.color = new Color(Rayo.r, Rayo.g, Rayo.b, rayo * 0.8f);

            if (sigue) return;

            // ---- se acabo: se va desvaneciendo
            if (_grupo != null)
            {
                _grupo.alpha -= Time.deltaTime * 3.5f;
                if (_grupo.alpha > 0f) return;
            }
            Apaga();
        }

        /// <summary>Alpha del velo segun la tabla de latidos, interpolando.</summary>
        static float Fogonazo(float t)
        {
            var ultimo = Latidos[Latidos.Length - 1];
            if (t >= ultimo.t) return ultimo.a;

            for (int i = 1; i < Latidos.Length; i++)
            {
                if (t > Latidos[i].t) continue;
                float k = Mathf.InverseLerp(Latidos[i - 1].t, Latidos[i].t, t);
                return Mathf.Lerp(Latidos[i - 1].a, Latidos[i].a, k);
            }
            return ultimo.a;
        }

        void Apaga()
        {
            _activo = false;
            if (_grupo != null) _grupo.alpha = 0f;
        }

        // ---------------------------------------------------------- montaje

        void Construye()
        {
            // suelto en la escena: si cuelga del 10_HUD queda dentro del Canvas
            // de la barra de vida, y un Canvas anidado ni se estira a la
            // pantalla ni respeta el CanvasScaler
            var raiz = new GameObject("UI_TiempoDetenido", typeof(RectTransform));
            _raiz = raiz;

            _canvas = raiz.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 350;                 // encima del HUD, debajo de la pausa

            var escala = raiz.AddComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920f, 1080f);
            escala.matchWidthOrHeight = 0.5f;

            _grupo = raiz.AddComponent<CanvasGroup>();
            _grupo.blocksRaycasts = false;
            _grupo.interactable = false;

            _tinte = Caja("Tinte", new Color(Rayo.r, Rayo.g, Rayo.b, 0f), raiz.transform);
            Pantalla(_tinte.rectTransform);

            // dos franjas de luz arriba y abajo: dan el golpe de "chispazo"
            _brilloArriba = Caja("Rayo_Arriba", new Color(Rayo.r, Rayo.g, Rayo.b, 0f), raiz.transform);
            Franja(_brilloArriba.rectTransform, 1f);
            _brilloAbajo = Caja("Rayo_Abajo", new Color(Rayo.r, Rayo.g, Rayo.b, 0f), raiz.transform);
            Franja(_brilloAbajo.rectTransform, 0f);

            var bloque = new GameObject("Bloque", typeof(RectTransform));
            bloque.transform.SetParent(raiz.transform, false);
            _bloque = (RectTransform)bloque.transform;
            _bloque.anchorMin = _bloque.anchorMax = new Vector2(0.5f, 0.68f);
            _bloque.sizeDelta = new Vector2(1200f, 220f);
            _bloque.anchoredPosition = Vector2.zero;

            // una plancha oscura detras del bloque: el fogonazo tine toda la
            // pantalla del mismo color que las letras, y sin algo oscuro debajo
            // el rotulo se funde con el destello justo en el instante en que
            // mas hace falta leerlo
            _placa = Caja("Plancha", new Color(0.03f, 0.02f, 0.08f, 0.55f), _bloque);
            Centrado(_placa.rectTransform, new Vector2(1180f, 210f), new Vector2(0f, 0f));

            _sombra = Texto("RotuloSombra", "TIEMPO DETENIDO", 86f,
                            new Color(0.02f, 0.03f, 0.09f), _bloque,
                            new Vector2(5f, 35f), new Vector2(1200f, 110f));
            _rotulo = Texto("Rotulo", "TIEMPO DETENIDO", 86f, Rayo, _bloque,
                            new Vector2(0f, 40f), new Vector2(1200f, 110f));

            // ---- la barra que se vacia
            var marco = Caja("BarraMarco", new Color(Rayo.r, Rayo.g, Rayo.b, 0.9f), _bloque);
            Centrado(marco.rectTransform, new Vector2(620f, 20f), new Vector2(0f, -40f));

            var fondo = Caja("BarraFondo", Oscuro, marco.transform);
            Dentro(fondo.rectTransform, 3f);

            _relleno = Caja("BarraRelleno", Rayo, fondo.transform);
            Dentro(_relleno.rectTransform, 0f);
            var rt = _relleno.rectTransform;
            rt.pivot = new Vector2(0f, 0.5f);

            _pie = Texto("Restante", "", 26f, Cian, _bloque,
                         new Vector2(0f, -78f), new Vector2(620f, 34f));
        }

        Image Caja(string nombre, Color c, Transform padre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
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
            Centrado((RectTransform)go.transform, size, pos);
            return t;
        }

        static void Centrado(RectTransform rt, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        static void Pantalla(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Franja(RectTransform rt, float arriba)
        {
            rt.anchorMin = new Vector2(0f, arriba);
            rt.anchorMax = new Vector2(1f, arriba);
            rt.pivot = new Vector2(0.5f, arriba);
            rt.sizeDelta = new Vector2(0f, 26f);
            rt.anchoredPosition = Vector2.zero;
        }

        static void Dentro(RectTransform rt, float margen)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margen, margen);
            rt.offsetMax = new Vector2(-margen, -margen);
        }
    }
}
