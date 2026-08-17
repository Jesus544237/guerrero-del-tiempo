using System.Collections.Generic;
using Ekkar.Core;
using Ekkar.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ekkar.UI
{
    /// <summary>
    /// El menu que sale a mitad de partida: para el juego y deja ver donde
    /// estabas, pero en gris.
    ///
    /// El gris no es un filtro en vivo — eso pide una pasada extra de camara en
    /// URP y no compensa para una pantalla parada. Se hace lo unico que tiene
    /// sentido cuando el juego esta congelado: se le pide a la camara un
    /// dibujado suelto, se le quita el color y se deja de fondo. Como el mundo
    /// no se mueve, esa foto y el mundo real son la misma imagen.
    ///
    /// Se navega entero con el teclado: arriba y abajo para cambiar de linea,
    /// izquierda y derecha para tocar el ajuste, ENTER para las acciones. El
    /// raton tambien vale.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] internal TMP_FontAsset fuente;
        [Tooltip("Si en tu resolucion queda grande o pequeno, se toca aqui.")]
        [SerializeField] Vector2 tamanoPanel = new Vector2(1420f, 840f);

        static readonly Color Morado = new Color(0.176f, 0.106f, 0.412f);
        static readonly Color Violeta = new Color(0.486f, 0.227f, 0.929f);
        static readonly Color Cian = new Color(0.133f, 0.827f, 0.929f);
        static readonly Color Oro = new Color(0.980f, 0.753f, 0.141f);
        static readonly Color Hueso = new Color(0.886f, 0.906f, 0.922f);
        static readonly Color Tenue = new Color(0.62f, 0.60f, 0.74f);

        /// <summary>Para que nadie mas lea el teclado mientras esta abierto.</summary>
        public static bool Abierta { get; private set; }

        // ---- filas navegables ------------------------------------------

        enum Tipo { Ajuste, Accion }

        class Fila
        {
            public Tipo tipo;
            public TextMeshProUGUI etiqueta, valor;
            public Image marco;
            public System.Func<string> lee;
            public System.Action<int> cambia;    // -1 izquierda, +1 derecha
            public System.Action activa;
        }

        readonly List<Fila> _filas = new List<Fila>();
        int _seleccion;

        GameObject _panel, _raiz;
        Canvas _canvas;
        RawImage _foto;
        Texture2D _grisTex;
        bool _abierto;
        float _repeticion;

        GameObject[] _paginas;
        Image[] _pestanas;
        TextMeshProUGUI[] _textoPestanas;
        int _pagina;

        bool _cursorAntes;
        CursorLockMode _bloqueoAntes;

        void Start()
        {
            Construye();
            _panel.SetActive(false);
            if (!GameSettings.Loaded) GameSettings.Load();
        }

        void OnDestroy()
        {
            if (_abierto) Time.timeScale = 1f;
            Abierta = false;
            LimpiaFoto();
            if (_raiz != null) Destroy(_raiz);
        }

        void Update()
        {
            if (PausaPulsada() && !HayResultado())
            {
                if (_abierto) Cierra();
                else Abre();
                return;
            }

            if (!_abierto) return;
            Navega();
        }

        /// <summary>
        /// Con la pantalla de derrota o de victoria delante no se pausa: esa ya
        /// para el juego por su cuenta y tiene sus propios botones.
        /// </summary>
        bool HayResultado()
        {
            if (_abierto) return false;
            var res = FindAnyObjectByType<ResultScreen>();
            return res != null && res.IsShown;
        }

        // ------------------------------------------------------------ abrir

        void Abre()
        {
            CapturaEnGris();

            _abierto = true;
            Abierta = true;
            _panel.SetActive(true);
            _seleccion = 0;
            MuestraPagina(0);
            Time.timeScale = 0f;

            // el raton hace falta aqui aunque el juego lo esconda mientras juegas
            _cursorAntes = Cursor.visible;
            _bloqueoAntes = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            Audio.Sfx.Play("espada_out", 0.5f, 1.2f);
        }

        void Cierra()
        {
            _abierto = false;
            Abierta = false;
            _panel.SetActive(false);
            Time.timeScale = 1f;
            LimpiaFoto();

            Cursor.visible = _cursorAntes;
            Cursor.lockState = _bloqueoAntes;

            Audio.Sfx.Play("espada_out", 0.5f, 0.8f);
        }

        /// <summary>
        /// La foto del momento, sin color.
        ///
        /// Se le pide a la camara del mundo que dibuje una vez en una textura
        /// aparte, en vez de copiar lo que hay en pantalla. Sale mejor por dos
        /// razones: no hay que esperar al final del fotograma — y esa espera en
        /// el editor no llega si la pestana Game no esta a la vista, con lo que
        /// el menu se quedaba sin abrir —, y en la textura no entra ningun
        /// Canvas, asi que el HUD no aparece duplicado dentro del gris.
        ///
        /// Se dibuja a 720 de alto: es un fondo detras de un panel, no hace
        /// falta pagar 4K para convertirlo pixel a pixel.
        /// </summary>
        void CapturaEnGris()
        {
            LimpiaFoto();

            var cam = Camera.main;
            if (cam == null)
            {
                if (_foto != null) _foto.color = new Color(0.22f, 0.22f, 0.24f, 1f);
                return;
            }

            int alto = 720;
            int ancho = Mathf.Max(2, Mathf.RoundToInt(alto * (Screen.width / (float)Mathf.Max(1, Screen.height))));

            var rt = RenderTexture.GetTemporary(ancho, alto, 24, RenderTextureFormat.ARGB32);
            if (!Dibuja(cam, rt))
            {
                RenderTexture.ReleaseTemporary(rt);
                if (_foto != null) _foto.color = new Color(0.22f, 0.22f, 0.24f, 1f);
                return;
            }

            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            _grisTex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _grisTex.ReadPixels(new Rect(0f, 0f, ancho, alto), 0, 0, false);
            RenderTexture.active = antes;
            RenderTexture.ReleaseTemporary(rt);

            var px = _grisTex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                // luminancia de toda la vida, y un punto mas oscura para que el
                // panel de encima se lea sin esfuerzo
                int g = (c.r * 77 + c.g * 150 + c.b * 29) >> 8;
                byte v = (byte)Mathf.Clamp(g * 82 / 100 + 10, 0, 255);
                px[i] = new Color32(v, v, v, 255);
            }
            _grisTex.SetPixels32(px);
            _grisTex.Apply(false, false);

            if (_foto != null)
            {
                _foto.texture = _grisTex;
                _foto.color = Color.white;
            }
        }

        /// <summary>
        /// Un unico dibujado de la camara a la textura. En URP hay que pedirlo
        /// con una peticion de render; Camera.Render() a secas queda como plan B
        /// por si algun dia el proyecto vuelve al pipeline clasico.
        /// </summary>
        static bool Dibuja(Camera cam, RenderTexture destino)
        {
            try
            {
                var peticion = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = destino };
                cam.SubmitRenderRequest(peticion);
                return true;
            }
            catch { /* sin pipeline moderno: se intenta a la antigua */ }

            try
            {
                var previo = cam.targetTexture;
                cam.targetTexture = destino;
                cam.Render();
                cam.targetTexture = previo;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Ekkar] No pude dibujar la escena para el fondo gris: {e.Message}");
                return false;
            }
        }

        void LimpiaFoto()
        {
            if (_foto != null) _foto.texture = null;
            if (_grisTex == null) return;
            Destroy(_grisTex);
            _grisTex = null;
        }

        // --------------------------------------------------------- navegar

        void Navega()
        {
            if (TabPulsado()) { MuestraPagina(_pagina == 0 ? 1 : 0); return; }

            // la pagina de "como se juega" solo se lee: el teclado ahi no toca nada
            if (_pagina != 0 || _filas.Count == 0) return;

            int vertical = 0, horizontal = 0;
            bool acepta = false;

            if (ArribaPulsada()) vertical = -1;
            else if (AbajoPulsada()) vertical = 1;
            if (IzquierdaPulsada()) horizontal = -1;
            else if (DerechaPulsada()) horizontal = 1;
            if (AceptarPulsado()) acepta = true;

            // mantener pulsada la flecha repite el ajuste, que si no mover el
            // volumen de 0 a 100 son veinte pulsaciones
            if (horizontal == 0 && MantieneHorizontal(out int mantenido))
            {
                _repeticion -= Time.unscaledDeltaTime;
                if (_repeticion <= 0f) { horizontal = mantenido; _repeticion = 0.07f; }
            }
            else if (horizontal != 0) _repeticion = 0.32f;

            if (vertical != 0)
            {
                _seleccion = (_seleccion + vertical + _filas.Count) % _filas.Count;
                Audio.Sfx.Play("paso", 0.35f, 1.4f);
                Refresca();
            }

            var fila = _filas[Mathf.Clamp(_seleccion, 0, _filas.Count - 1)];

            if (horizontal != 0 && fila.tipo == Tipo.Ajuste)
            {
                fila.cambia?.Invoke(horizontal);
                GameSettings.Save();
                Audio.Sfx.Play("paso", 0.4f, 1.7f);
                Refresca();
            }

            if (acepta && fila.tipo == Tipo.Accion)
            {
                Audio.Sfx.Play("espada_out", 0.6f, 1.1f);
                fila.activa?.Invoke();
            }
        }

        void Refresca()
        {
            for (int i = 0; i < _filas.Count; i++)
            {
                var f = _filas[i];
                bool sel = i == _seleccion;
                if (f.valor != null && f.lee != null) f.valor.text = f.lee();
                if (f.etiqueta != null) f.etiqueta.color = sel ? Oro : Hueso;
                if (f.valor != null) f.valor.color = sel ? Oro : Cian;
                if (f.marco != null)
                    f.marco.color = sel ? Violeta : new Color(Violeta.r, Violeta.g, Violeta.b, 0.22f);
            }
        }

        // --------------------------------------------------------- acciones

        /// <summary>Salta a la otra pagina, lo mismo que hace TAB.</summary>
        public void PaginaSiguiente() => MuestraPagina(_pagina == 0 ? 1 : 0);

        /// <summary>Abre o cierra, lo mismo que hace la tecla ESC.</summary>
        public void Alterna()
        {
            if (_abierto) Cierra();
            else Abre();
        }

        public void Continuar()
        {
            if (_abierto) Cierra();
        }

        public void Reintentar()
        {
            Time.timeScale = 1f;
            Abierta = false;
            TimeControl.Reiniciar();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void AlMenu()
        {
            Time.timeScale = 1f;
            Abierta = false;
            TimeControl.Reiniciar();
            if (Application.CanStreamedLevelBeLoaded("MainMenu")) SceneManager.LoadScene("MainMenu");
            else Debug.LogWarning("[Ekkar] MainMenu no esta en la lista de escenas del build.");
        }

        // ---------------------------------------------------------- montaje

        void Construye()
        {
            // suelto en la escena, no colgando del 10_HUD: ese objeto ya lleva
            // el Canvas de la barra de vida, y un Canvas dentro de otro deja de
            // ser raiz — Unity ya no le estira el rectangulo a la pantalla ni le
            // hace caso al CanvasScaler, y el panel acababa en una esquina y al
            // doble de tamano
            var raiz = new GameObject("UI_Pausa", typeof(RectTransform));
            _raiz = raiz;

            _canvas = raiz.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;                 // por encima de todo

            var escala = raiz.AddComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920f, 1080f);
            escala.matchWidthOrHeight = 0.5f;
            raiz.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("Pausa", typeof(RectTransform));
            _panel.transform.SetParent(raiz.transform, false);
            Pantalla((RectTransform)_panel.transform);

            // el fotograma en gris, al fondo del todo
            var fotoGo = new GameObject("EscenaEnGris", typeof(RectTransform));
            fotoGo.transform.SetParent(_panel.transform, false);
            _foto = fotoGo.AddComponent<RawImage>();
            _foto.color = new Color(0.24f, 0.24f, 0.26f, 1f);
            _foto.raycastTarget = false;
            Pantalla(_foto.rectTransform);

            // un velo muy suave encima: separa el panel del fondo sin taparlo
            var velo = Caja("Velo", new Color(0.04f, 0.02f, 0.08f, 0.42f), _panel.transform);
            Pantalla(velo.rectTransform);

            var marco = Caja("Marco", Violeta, _panel.transform);
            Centro(marco.rectTransform, tamanoPanel);

            var fondo = Caja("Fondo", new Color(Morado.r, Morado.g, Morado.b, 0.97f), marco.transform);
            Dentro(fondo.rectTransform, 4f);

            float mitad = tamanoPanel.y * 0.5f;

            Texto("Titulo", "PAUSA", 58f, Oro, fondo.transform,
                  new Vector2(0f, mitad - 58f), new Vector2(900f, 72f), TextAlignmentOptions.Center);

            // ---- las dos pestanas
            _pestanas = new Image[2];
            _textoPestanas = new TextMeshProUGUI[2];
            Pestana(0, "JUEGO Y AJUSTES", -170f, mitad - 116f, fondo.transform);
            Pestana(1, "COMO SE JUEGA", 170f, mitad - 116f, fondo.transform);

            // ---- pagina 1
            _paginas = new GameObject[2];
            _paginas[0] = Pagina("Pagina_Ajustes", fondo.transform);
            ColumnaControles(_paginas[0].transform, mitad);
            ColumnaAjustes(_paginas[0].transform, mitad);

            // ---- pagina 2
            _paginas[1] = Pagina("Pagina_Juego", fondo.transform);
            PaginaDelJuego(_paginas[1].transform, mitad);

            Texto("Pista", "RATON o FLECHAS para moverte     TAB cambia de pagina     ESC vuelve al juego",
                  18f, Tenue, fondo.transform,
                  new Vector2(0f, -mitad + 30f), new Vector2(1300f, 26f), TextAlignmentOptions.Center);

            MuestraPagina(0);
        }

        GameObject Pagina(string nombre, Transform padre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            Pantalla((RectTransform)go.transform);
            return go;
        }

        void Pestana(int indice, string texto, float x, float y, Transform padre)
        {
            var marco = Caja("Pestana_" + indice, new Color(Violeta.r, Violeta.g, Violeta.b, 0.25f), padre);
            var rt = marco.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, 40f);
            rt.anchoredPosition = new Vector2(x, y);
            marco.raycastTarget = true;

            var t = Texto("Texto", texto, 21f, Hueso, marco.transform, Vector2.zero,
                          new Vector2(300f, 28f), TextAlignmentOptions.Center);

            int cual = indice;
            var b = marco.gameObject.AddComponent<Button>();
            b.targetGraphic = marco;
            b.onClick.AddListener(() => MuestraPagina(cual));

            _pestanas[indice] = marco;
            _textoPestanas[indice] = t;
        }

        void MuestraPagina(int cual)
        {
            _pagina = Mathf.Clamp(cual, 0, _paginas.Length - 1);
            for (int i = 0; i < _paginas.Length; i++)
            {
                if (_paginas[i] != null) _paginas[i].SetActive(i == _pagina);
                if (_pestanas[i] != null)
                    _pestanas[i].color = i == _pagina
                        ? Violeta
                        : new Color(Violeta.r, Violeta.g, Violeta.b, 0.25f);
                if (_textoPestanas[i] != null) _textoPestanas[i].color = i == _pagina ? Oro : Hueso;
            }
            Refresca();
        }

        /// <summary>
        /// La pagina de "de que va esto". Un jugador que abre la pausa por
        /// primera vez no sabe ni cual es el objetivo del nivel ni que el mana
        /// se llena pegando: eso no estaba escrito en ninguna parte del juego.
        /// </summary>
        void PaginaDelJuego(Transform padre, float mitad)
        {
            float izq = -tamanoPanel.x * 0.245f;
            float der = tamanoPanel.x * 0.245f;
            float anchoCol = tamanoPanel.x * 0.44f;

            Bloque(padre, izq, mitad - 170f, anchoCol, "LA HISTORIA", new[]
            {
                "El Gran Reloj que mantenia en orden las eras",
                "se ha roto, y sus fragmentos han caido en la",
                "Edad Media, la Era Industrial y el Futuro.",
                "Sin ellos, cada epoca se deshace por su cuenta.",
                "",
                "Ekkar es el ultimo Guerrero del Tiempo. Cruza",
                "las tres eras para recuperar los fragmentos",
                "antes de la Hora Cero: el instante en que el",
                "tiempo deja de existir.",
            });

            Bloque(padre, izq, mitad - 460f, anchoCol, "TU OBJETIVO", new[]
            {
                "Avanza hasta el final de cada era y derrota a",
                "su campeon para llevarte el fragmento.",
                "Con los tres, la Hora Cero se abre y te espera",
                "el Senor del Tiempo.",
                "",
                "Las hogueras temporales guardan tu avance:",
                "si caes, vuelves a la ultima que encendiste.",
            });

            Bloque(padre, der, mitad - 170f, anchoCol, "COMO SE PELEA", new[]
            {
                "Golpear con J llena la barra de mana. El mana",
                "es lo unico que paga las habilidades, y pegar",
                "normal nunca lo gasta.",
                "",
                "Los enemigos solo alcanzan lo que tienen",
                "DELANTE y a su altura: pasa por detras, salta",
                "por encima o aparta con el dash.",
                "",
                "El salto doble suelta una tormenta de rayos",
                "que muerde a todo lo que tengas alrededor.",
                "Cuesta mana: sin el, el salto sale seco.",
            });

            Bloque(padre, der, mitad - 470f, anchoCol, "LAS HABILIDADES", new[]
            {
                "SHIFT  dash: embiste y hace dano al atravesar.",
                "E  detiene el tiempo: enemigos y disparos se",
                "   quedan clavados, tu no.",
                "K  golpe cargado: mas dano y mas alcance.",
                "R  chronobreak: deja en los huesos a todo lo",
                "   cercano, y remata a los que ya iban mal.",
            });
        }

        void Bloque(Transform padre, float x, float y, float ancho, string titulo, string[] lineas)
        {
            Texto("T_" + titulo, titulo, 24f, Violeta, padre,
                  new Vector2(x, y), new Vector2(ancho, 30f), TextAlignmentOptions.Left);

            float yy = y - 34f;
            foreach (var l in lineas)
            {
                if (!string.IsNullOrEmpty(l))
                    Texto("L", l, 19f, Hueso, padre,
                          new Vector2(x, yy), new Vector2(ancho, 24f), TextAlignmentOptions.Left);
                yy -= 24f;
            }
        }

        /// <summary>
        /// Los controles, sacados de los propios componentes para que los costes
        /// que se leen aqui sean los que de verdad cobra el juego.
        /// </summary>
        void ColumnaControles(Transform padre, float mitad)
        {
            var ekkar = FindAnyObjectByType<EkkarController>();
            var combate = ekkar != null ? ekkar.GetComponent<PlayerCombat>() : null;
            int mDash = ekkar != null ? ekkar.ManaDash : 1;
            int mDetener = ekkar != null ? ekkar.ManaDetener : 3;
            int mChrono = ekkar != null ? ekkar.ManaChrono : 6;
            int mCarga = combate != null ? combate.CosteCargado : 4;
            int mTormenta = ekkar != null ? ekkar.ManaTormenta : 2;

            var lista = new (string tecla, string accion)[]
            {
                ("A  D   /   flechas", "Moverse"),
                ("ESPACIO  o  W", "Saltar"),
                ("ESPACIO  x2", $"Salto doble + tormenta   ({mTormenta} de mana)"),
                ("SHIFT", $"Dash   ({mDash} de mana)"),
                ("J", "Atacar"),
                ("I + J", "Ataque hacia arriba"),
                ("J  en el aire", "Ataque en salto"),
                ("K", $"Golpe cargado   ({mCarga} de mana)"),
                ("E", $"Detener el tiempo   ({mDetener} de mana)"),
                ("R", $"Chronobreak   ({mChrono} de mana)"),
                ("Q", "Envainar / desenvainar"),
                ("ESC  o  P", "Pausa"),
            };

            float x = -tamanoPanel.x * 0.25f;
            Texto("TituloControles", "CONTROLES", 26f, Violeta, padre,
                  new Vector2(x, mitad - 170f), new Vector2(520f, 32f), TextAlignmentOptions.Center);

            float y = mitad - 214f;
            foreach (var (tecla, accion) in lista)
            {
                // la tecla acaba a la izquierda del hueco y la accion empieza a
                // la derecha: sin ese hueco, "J en el aire" se pegaba a su
                // propia descripcion y no se sabia donde acababa una cosa
                Texto("T", tecla, 20f, Oro, padre,
                      new Vector2(x - 138f, y), new Vector2(254f, 26f), TextAlignmentOptions.MidlineRight);
                Texto("A", accion, 20f, Hueso, padre,
                      new Vector2(x + 155f, y), new Vector2(290f, 26f), TextAlignmentOptions.MidlineLeft);
                y -= 30f;
            }
        }

        void ColumnaAjustes(Transform padre, float mitad)
        {
            float x = tamanoPanel.x * 0.25f;
            Texto("TituloAjustes", "AJUSTES", 26f, Violeta, padre,
                  new Vector2(x, mitad - 170f), new Vector2(520f, 32f), TextAlignmentOptions.Center);

            float y = mitad - 220f;

            Ajuste(padre, x, ref y, "MUSICA",
                   () => Barra(GameSettings.MusicVolume),
                   d =>
                   {
                       GameSettings.MusicVolume = Mathf.Clamp01(GameSettings.MusicVolume + d * 0.1f);
                       GameSettings.ApplyAudio();
                   });

            Ajuste(padre, x, ref y, "EFECTOS",
                   () => Barra(GameSettings.SfxVolume),
                   d =>
                   {
                       GameSettings.SfxVolume = Mathf.Clamp01(GameSettings.SfxVolume + d * 0.1f);
                       Audio.Sfx.Volumen = GameSettings.SfxVolume * 0.75f;
                       GameSettings.ApplyAudio();
                   });

            Ajuste(padre, x, ref y, "PANTALLA",
                   () => GameSettings.Mode switch
                   {
                       ScreenMode.Fullscreen => "COMPLETA",
                       ScreenMode.Borderless => "SIN BORDES",
                       _ => "VENTANA",
                   },
                   d =>
                   {
                       int n = System.Enum.GetValues(typeof(ScreenMode)).Length;
                       GameSettings.Mode = (ScreenMode)(((int)GameSettings.Mode + d + n) % n);
                       GameSettings.ApplyGraphics();
                   });

            Ajuste(padre, x, ref y, "VSYNC",
                   () => GameSettings.VSync ? "SI" : "NO",
                   _ => { GameSettings.VSync = !GameSettings.VSync; GameSettings.ApplyGraphics(); });

            Ajuste(padre, x, ref y, "PARTICULAS",
                   () => GameSettings.Particles ? "SI" : "NO",
                   _ => { GameSettings.Particles = !GameSettings.Particles; GameSettings.ApplyFx(); });

            y -= 24f;
            Accion(padre, x, ref y, "CONTINUAR", Continuar);
            Accion(padre, x, ref y, "REINTENTAR NIVEL", Reintentar);
            Accion(padre, x, ref y, "MENU PRINCIPAL", AlMenu);
        }

        static string Barra(float v)
        {
            int llenos = Mathf.RoundToInt(Mathf.Clamp01(v) * 10f);
            return new string('|', llenos).PadRight(10, '.') + $"  {Mathf.RoundToInt(v * 100)}%";
        }

        void Ajuste(Transform padre, float x, ref float y, string nombre,
                    System.Func<string> lee, System.Action<int> cambia)
        {
            var marco = Caja("Fila_" + nombre, new Color(Violeta.r, Violeta.g, Violeta.b, 0.22f), padre);
            var rt = marco.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(520f, 40f);
            rt.anchoredPosition = new Vector2(x, y);

            var fondo = Caja("Fondo", new Color(0.06f, 0.03f, 0.12f, 0.75f), marco.transform);
            Dentro(fondo.rectTransform, 2f);

            var etiqueta = Texto("Nombre", nombre, 21f, Hueso, fondo.transform,
                                 new Vector2(14f, 0f), new Vector2(220f, 30f), TextAlignmentOptions.MidlineLeft);
            etiqueta.rectTransform.anchorMin = etiqueta.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            etiqueta.rectTransform.pivot = new Vector2(0f, 0.5f);

            // el valor termina a la izquierda de las dos flechas: si no, el
            // texto se mete por debajo de ellas y no se lee ninguno de los dos
            var valor = Texto("Valor", "", 21f, Cian, fondo.transform,
                              new Vector2(-152f, 0f), new Vector2(240f, 30f), TextAlignmentOptions.MidlineRight);
            valor.rectTransform.anchorMin = valor.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            valor.rectTransform.pivot = new Vector2(1f, 0.5f);

            int indice = _filas.Count;
            var fila = new Fila
            {
                tipo = Tipo.Ajuste, marco = marco, etiqueta = etiqueta,
                valor = valor, lee = lee, cambia = cambia,
            };
            _filas.Add(fila);

            // las dos flechas: sin ellas el raton no podia tocar un ajuste,
            // solo mirarlo
            Flecha(fondo.transform, "<", -110f, indice, -1);
            Flecha(fondo.transform, ">", -14f, indice, +1);

            AlPasarElRaton(marco, indice);
            y -= 46f;
        }

        /// <summary>Una de las dos flechitas que mueven un ajuste con el raton.</summary>
        void Flecha(Transform padre, string signo, float x, int indice, int sentido)
        {
            var caja = Caja("Flecha" + signo, new Color(Violeta.r, Violeta.g, Violeta.b, 0.5f), padre);
            var rt = caja.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(34f, 30f);
            rt.anchoredPosition = new Vector2(x, 0f);
            caja.raycastTarget = true;

            Texto("Signo", signo, 22f, Hueso, caja.transform, Vector2.zero,
                  new Vector2(32f, 28f), TextAlignmentOptions.Center);

            var b = caja.gameObject.AddComponent<Button>();
            b.targetGraphic = caja;
            var colores = b.colors;
            colores.highlightedColor = Cian;
            colores.pressedColor = Oro;
            b.colors = colores;
            b.onClick.AddListener(() =>
            {
                _seleccion = indice;
                var f = _filas[indice];
                f.cambia?.Invoke(sentido);
                GameSettings.Save();
                Audio.Sfx.Play("paso", 0.4f, 1.7f);
                Refresca();
            });
        }

        /// <summary>Pasar el raton por encima selecciona la fila, como en el menu.</summary>
        void AlPasarElRaton(Image objetivo, int indice)
        {
            objetivo.raycastTarget = true;
            var trigger = objetivo.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var entrada = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            entrada.callback.AddListener(_ => { _seleccion = indice; Refresca(); });
            trigger.triggers.Add(entrada);
        }

        void Accion(Transform padre, float x, ref float y, string nombre, UnityEngine.Events.UnityAction accion)
        {
            var marco = Caja("Boton_" + nombre, new Color(Violeta.r, Violeta.g, Violeta.b, 0.22f), padre);
            var rt = marco.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(520f, 46f);
            rt.anchoredPosition = new Vector2(x, y);

            var fondo = Caja("Fondo", new Color(0.06f, 0.03f, 0.12f, 0.85f), marco.transform);
            Dentro(fondo.rectTransform, 2f);

            var etiqueta = Texto("Texto", nombre, 24f, Hueso, fondo.transform, Vector2.zero,
                                 new Vector2(480f, 32f), TextAlignmentOptions.Center);

            marco.raycastTarget = true;
            var b = marco.gameObject.AddComponent<Button>();
            b.targetGraphic = marco;
            b.onClick.AddListener(accion);
            var colores = b.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = Cian;
            colores.pressedColor = Oro;
            colores.selectedColor = Cian;
            b.colors = colores;

            int indice = _filas.Count;
            var trigger = marco.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var entrada = new UnityEngine.EventSystems.EventTrigger.Entry
            { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            entrada.callback.AddListener(_ => { _seleccion = indice; Refresca(); });
            trigger.triggers.Add(entrada);

            _filas.Add(new Fila { tipo = Tipo.Accion, marco = marco, etiqueta = etiqueta, activa = () => accion() });
            y -= 52f;
        }

        // ------------------------------------------------------------ piezas

        static Image Caja(string nombre, Color c, Transform padre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI Texto(string nombre, string texto, float tam, Color color,
                              Transform padre, Vector2 pos, Vector2 size, TextAlignmentOptions al)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (fuente != null) t.font = fuente;
            t.text = texto;
            t.fontSize = tam;
            t.color = color;
            t.alignment = al;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return t;
        }

        static void Pantalla(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Centro(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        static void Dentro(RectTransform rt, float margen)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margen, margen);
            rt.offsetMax = new Vector2(-margen, -margen);
        }

        // ----------------------------------------------------------- entrada

#if ENABLE_INPUT_SYSTEM
        static bool PausaPulsada()
        {
            var k = Keyboard.current;
            return k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame);
        }
        static bool IzquierdaPulsada() => Pulsa(k => k.leftArrowKey) || Pulsa(k => k.aKey);
        static bool DerechaPulsada() => Pulsa(k => k.rightArrowKey) || Pulsa(k => k.dKey);
        static bool ArribaPulsada() => Pulsa(k => k.upArrowKey) || Pulsa(k => k.wKey);
        static bool AbajoPulsada() => Pulsa(k => k.downArrowKey) || Pulsa(k => k.sKey);
        static bool AceptarPulsado() => Pulsa(k => k.enterKey) || Pulsa(k => k.numpadEnterKey) || Pulsa(k => k.spaceKey);
        static bool TabPulsado() => Pulsa(k => k.tabKey);

        static bool Pulsa(System.Func<Keyboard, UnityEngine.InputSystem.Controls.KeyControl> sel)
        {
            var k = Keyboard.current;
            return k != null && sel(k).wasPressedThisFrame;
        }

        static bool MantieneHorizontal(out int sentido)
        {
            sentido = 0;
            var k = Keyboard.current;
            if (k == null) return false;
            if (k.leftArrowKey.isPressed || k.aKey.isPressed) sentido = -1;
            else if (k.rightArrowKey.isPressed || k.dKey.isPressed) sentido = 1;
            return sentido != 0;
        }
#else
        static bool PausaPulsada() => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
        static bool IzquierdaPulsada() => Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        static bool DerechaPulsada() => Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        static bool ArribaPulsada() => Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        static bool AbajoPulsada() => Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        static bool AceptarPulsado() => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
        static bool TabPulsado() => Input.GetKeyDown(KeyCode.Tab);

        static bool MantieneHorizontal(out int sentido)
        {
            sentido = 0;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) sentido = -1;
            else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) sentido = 1;
            return sentido != 0;
        }
#endif
    }
}
