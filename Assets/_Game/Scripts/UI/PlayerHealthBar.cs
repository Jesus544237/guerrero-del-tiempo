using Ekkar.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.UI
{
    /// <summary>
    /// La vida de Ekkar en la esquina, con la tipografia pixel del menu.
    ///
    /// Se monta sola al arrancar: crea su canvas, el marco, el relleno y las
    /// muescas que separan cada punto de vida. Al recibir un golpe el relleno
    /// baja de golpe y un fantasma blanco lo persigue con retraso, que es el
    /// truco de siempre para que se lea cuanto dano acabas de comer.
    /// </summary>
    public class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField] internal TMP_FontAsset fuente;
        [SerializeField] Vector2 tamano = new Vector2(360f, 26f);
        [SerializeField] Vector2 margen = new Vector2(40f, 34f);
        [SerializeField] Color colorVida = new Color(0.86f, 0.15f, 0.15f);
        [SerializeField] Color colorFondo = new Color(0.04f, 0.02f, 0.08f, 0.9f);
        [SerializeField] Color colorMarco = new Color(0.98f, 0.75f, 0.14f);

        Damageable _ekkar;
        Gameplay.PlayerCombat _combate;
        RectTransform _relleno, _fantasma, _mana, _brillo;
        TextMeshProUGUI _texto;
        float _objetivo = 1f, _suave = 1f;

        void Start()
        {
            foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                if (d.Lado == Damageable.Bando.Jugador) { _ekkar = d; break; }

            if (_ekkar == null) return;      // se reintenta en Update

            Construye();
            _ekkar.Danado += (_, queda) => _objetivo = queda / (float)_ekkar.VidaMaxima;
            _ekkar.Muerto_ += _ => _objetivo = 0f;
            Engancha();
            Refresca(1f);
        }

        void Construye()
        {
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;

            var escala = gameObject.GetComponent<CanvasScaler>();
            if (escala == null) escala = gameObject.AddComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920f, 1080f);

            var marco = Caja("Marco", colorMarco, canvas.transform);
            Coloca(marco, tamano + new Vector2(6f, 6f), margen - new Vector2(3f, 3f));

            var fondo = Caja("Fondo", colorFondo, marco.transform);
            Estira(fondo, 3f);

            _fantasma = Caja("Fantasma", new Color(1f, 1f, 1f, 0.45f), fondo.transform).rectTransform;
            Estira(_fantasma.GetComponent<Image>(), 0f);
            Ancla(_fantasma);

            var relleno = Caja("Vida", colorVida, fondo.transform);
            _relleno = relleno.rectTransform;
            Estira(relleno, 0f);
            Ancla(_relleno);

            // ---- barra de mana, justo debajo, en cian
            var marcoM = Caja("MarcoMana", new Color(0.13f, 0.83f, 0.93f, 0.9f), canvas.transform);
            Coloca(marcoM, new Vector2(tamano.x * 0.72f + 4f, 14f), margen + new Vector2(-3f, tamano.y + 8f));

            var fondoM = Caja("FondoMana", colorFondo, marcoM.transform);
            Estira(fondoM, 2f);

            var mana = Caja("Mana", new Color(0.13f, 0.83f, 0.93f), fondoM.transform);
            _mana = mana.rectTransform;
            Estira(mana, 0f);
            Ancla(_mana);
            _mana.localScale = new Vector3(0f, 1f, 1f);

            // destello que late cuando esta lleno
            var brillo = Caja("BrilloMana", new Color(1f, 1f, 1f, 0f), fondoM.transform);
            _brillo = brillo.rectTransform;
            Estira(brillo, 0f);

            var go = new GameObject("Texto", typeof(RectTransform));
            go.transform.SetParent(marco.transform, false);
            _texto = go.AddComponent<TextMeshProUGUI>();
            if (fuente != null) _texto.font = fuente;
            _texto.fontSize = 20f;
            _texto.color = new Color(0.886f, 0.906f, 0.922f);
            _texto.alignment = TextAlignmentOptions.MidlineLeft;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(2f, 8f);
            rt.sizeDelta = new Vector2(300f, 26f);
        }

        static Image Caja(string nombre, Color c, Transform padre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        void Coloca(Image img, Vector2 size, Vector2 pos)
        {
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
        }

        static void Estira(Image img, float dentro)
        {
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(dentro, dentro);
            rt.offsetMax = new Vector2(-dentro, -dentro);
        }

        static void Ancla(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
        }

        void Update()
        {
            if (_ekkar == null)
            {
                // Ekkar puede aparecer despues que el HUD (reaparicion,
                // carga diferida); se le espera en vez de rendirse
                foreach (var d in FindObjectsByType<Damageable>(FindObjectsSortMode.None))
                    if (d.Lado == Damageable.Bando.Jugador) { _ekkar = d; break; }
                if (_ekkar == null) return;
                if (_relleno == null) Construye();
                _ekkar.Danado += (_, queda) => _objetivo = queda / (float)_ekkar.VidaMaxima;
                _ekkar.Muerto_ += _ => _objetivo = 0f;
                Engancha();
            }
            ActualizaAviso();
            _suave = Mathf.MoveTowards(_suave, _objetivo, Time.deltaTime * 0.9f);
            Refresca(_objetivo);
        }

        static TextMeshProUGUI _aviso;
        static float _avisoHasta;

        /// <summary>Un rotulo grande en el centro, para las habilidades.</summary>
        public static void Aviso(string texto, Color color)
        {
            var barra = FindAnyObjectByType<PlayerHealthBar>();
            if (barra == null) return;
            var canvas = barra.GetComponent<Canvas>();
            if (canvas == null) return;

            if (_aviso == null)
            {
                var go = new GameObject("Aviso", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
                _aviso = go.AddComponent<TextMeshProUGUI>();
                if (barra.fuente != null) _aviso.font = barra.fuente;
                _aviso.fontSize = 64f;
                _aviso.alignment = TextAlignmentOptions.Center;
                _aviso.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.5f, 0.72f);
                rt.anchorMax = new Vector2(0.5f, 0.72f);
                rt.sizeDelta = new Vector2(1200f, 90f);
            }

            _aviso.text = texto;
            _aviso.color = color;
            _avisoHasta = Time.time + 1.6f;
            _aviso.gameObject.SetActive(true);
        }

        void ActualizaAviso()
        {
            if (_aviso == null || !_aviso.gameObject.activeSelf) return;
            float queda = _avisoHasta - Time.time;
            if (queda <= 0f) { _aviso.gameObject.SetActive(false); return; }
            var c = _aviso.color;
            c.a = Mathf.Clamp01(queda / 0.6f);
            _aviso.color = c;
        }

        void Engancha()
        {
            _combate = _ekkar.GetComponent<Gameplay.PlayerCombat>();
            if (_combate == null) return;

            _combate.ManaCambio += (m, max) => _manaObjetivo = max > 0 ? m / (float)max : 0f;

            // y el valor de ahora mismo: si el HUD arranca despues de que Ekkar
            // ya tenga mana, el evento de inicio se habria perdido y la barra
            // saldria vacia hasta el primer golpe
            _manaObjetivo = _combate.ManaMaximo > 0
                ? _combate.Mana / (float)_combate.ManaMaximo
                : 0f;
            _manaSuave = _manaObjetivo;
        }

        float _manaObjetivo, _manaSuave;

        void Refresca(float f)
        {
            if (_relleno == null) return;
            _relleno.localScale = new Vector3(Mathf.Clamp01(f), 1f, 1f);
            if (_fantasma != null) _fantasma.localScale = new Vector3(Mathf.Clamp01(_suave), 1f, 1f);
            if (_texto != null) _texto.text = $"EKKAR   {_ekkar.Vida}/{_ekkar.VidaMaxima}";

            // el mana sube suave y late cuando esta al maximo
            _manaSuave = Mathf.MoveTowards(_manaSuave, _manaObjetivo, Time.deltaTime * 2.2f);
            if (_mana != null) _mana.localScale = new Vector3(Mathf.Clamp01(_manaSuave), 1f, 1f);
            if (_brillo != null)
            {
                var img = _brillo.GetComponent<Image>();
                float a = _manaObjetivo >= 0.999f
                    ? 0.20f + Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 0.32f
                    : 0f;
                img.color = new Color(1f, 1f, 1f, a);
            }
        }
    }
}
