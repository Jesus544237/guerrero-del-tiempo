using System.Collections.Generic;
using System.IO;
using Ekkar.Audio;
using Ekkar.Core;
using Ekkar.FX;
using Ekkar.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Construye la escena completa del menu principal: camara, EventSystem,
    /// canvas, capas de fondo animadas, personaje, titulo, botones, paneles
    /// modales y transiciones. Se puede ejecutar tantas veces como haga falta:
    /// siempre genera la escena desde cero.
    /// </summary>
    public static class MainMenuBuilder
    {
        public const string SceneFolder = "Assets/_Game/Scenes";
        public const string ScenePath = SceneFolder + "/MainMenu.unity";
        const string ArtFolder = "Assets/_Game/Art";

        static Dictionary<string, Sprite> S;
        static TMP_FontAsset Font;      // pixel (solo ASCII)
        static TMP_FontAsset SansFont;  // prosa con tildes
        static Material MatTitle, MatAccent, MatBody, MatSans;
        static Sprite[] EkkarFrames;
        static Sprite Background;

        // -------------------------------------------------------- anclajes
        static readonly Vector2 ACenter = new Vector2(0.5f, 0.5f);
        static readonly Vector2 ALeft = new Vector2(0f, 0.5f);
        static readonly Vector2 ARight = new Vector2(1f, 0.5f);
        static readonly Vector2 ABottom = new Vector2(0.5f, 0f);
        static readonly Vector2 ABottomLeft = new Vector2(0f, 0f);
        static readonly Vector2 ABottomRight = new Vector2(1f, 0f);

        // ---------------------------------------------------------- paleta
        static readonly Color CFrameIdle = new Color(Palette.PurpleLight.r, Palette.PurpleLight.g, Palette.PurpleLight.b, 0.55f);
        static readonly Color CFillIdle = new Color(Palette.PurpleDeep.r, Palette.PurpleDeep.g, Palette.PurpleDeep.b, 0.78f);
        static readonly Color CFrameHot = Palette.CyanBright;
        static readonly Color CFillHot = new Color(Palette.Cyan.r, Palette.Cyan.g, Palette.Cyan.b, 0.30f);

        // ==================================================================

        public static void Build()
        {
            if (!LoadAssets()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MainMenu";

            CreateCamera();
            CreateEventSystem();

            var canvas = CreateCanvas();
            var canvasRt = (RectTransform)canvas.transform;

            var systems = new GameObject("_Sistemas");
            var audio = systems.AddComponent<AudioManager>();
            var controller = systems.AddComponent<MainMenuController>();

            // ---- capas
            var background = BuildBackground(canvasRt, out ParticleField embers, out ParticleField stars, out EnergyLines lines);
            var character = BuildCharacter(canvasRt);
            var menu = BuildMenu(canvasRt, out MenuButton btnPlay, out MenuButton btnOptions,
                                 out MenuButton btnCredits, out MenuButton btnQuit,
                                 out CanvasGroup titleGroup, out CanvasGroup dividerGroup,
                                 out TMPTextIntro[] titleIntros);
            var footer = BuildFooter(canvasRt);

            var panelsRoot = Rect("04_Paneles", canvasRt);
            Stretch(panelsRoot);
            var optionsPanel = BuildOptionsPanel(panelsRoot, controller, out OptionsPanel optionsLogic);
            var creditsPanel = BuildCreditsPanel(panelsRoot, controller);
            var quitPanel = BuildQuitPanel(panelsRoot, controller);

            var transition = BuildTransition(canvasRt);

            // ---- cableado del controlador
            WireController(controller, btnPlay, btnOptions, btnCredits, btnQuit,
                           optionsPanel, creditsPanel, quitPanel, transition,
                           menu.group, background, character, titleGroup, dividerGroup,
                           footer, titleIntros, embers, stars, lines);

            // ---- guardar
            Directory.CreateDirectory(SceneFolder);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            AddToBuildSettings();

            Debug.Log($"[Ekkar] Menu principal construido y guardado en {ScenePath}");
        }

        // ============================================================ assets

        static bool LoadAssets()
        {
            Font = EkkarFontBuilder.BuildFontAsset();
            if (Font == null)
            {
                Debug.LogError("[Ekkar] Sin fuente TMP no puedo construir el menu.");
                return false;
            }
            EkkarFontBuilder.BuildMaterials(Font);

            MatTitle = AssetDatabase.LoadAssetAtPath<Material>(EkkarFontBuilder.MatTitle);
            MatAccent = AssetDatabase.LoadAssetAtPath<Material>(EkkarFontBuilder.MatAccent);
            MatBody = AssetDatabase.LoadAssetAtPath<Material>(EkkarFontBuilder.MatBody);
            MatSans = AssetDatabase.LoadAssetAtPath<Material>(EkkarFontBuilder.MatSans);
            SansFont = EkkarFontBuilder.LoadSansFont();

            S = new Dictionary<string, Sprite>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { MenuArtGenerator.Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) S[Path.GetFileNameWithoutExtension(path)] = sp;
            }
            if (S.Count == 0)
            {
                Debug.LogError("[Ekkar] Faltan los sprites de interfaz. Ejecuta primero 'Regenerar arte de UI'.");
                return false;
            }

            Background = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/Backgrounds/bg_menu_prologue.png");

            var frames = new List<Sprite>();
            for (int i = 0; i < 32; i++)
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/Characters/Ekkar/ekkar_idle_{i:00}.png");
                if (sp == null) break;
                frames.Add(sp);
            }
            EkkarFrames = frames.ToArray();

            return true;
        }

        static Sprite Sp(string key) => S.TryGetValue(key, out var s) ? s : null;

        // ======================================================== primitivas

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = ACenter;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static Image Img(string name, Transform parent, Sprite sprite, Color color,
                         Image.Type type = Image.Type.Simple, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            img.raycastTarget = raycast;
            if (type == Image.Type.Sliced || type == Image.Type.Tiled) img.pixelsPerUnitMultiplier = 1f;
            return img;
        }

        static TextMeshProUGUI Text(string name, Transform parent, string content, float size,
                                    Color color, TextAlignmentOptions align, Material material = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = Font;
            if (material != null) tmp.fontSharedMaterial = material;
            tmp.text = content;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        /// <summary>
        /// Texto de prosa: usa Liberation Sans, que si tiene tildes y enes.
        /// La fuente pixel se reserva para rotulos en mayusculas sin acentos.
        /// </summary>
        static TextMeshProUGUI TextSans(string name, Transform parent, string content, float size,
                                        Color color, TextAlignmentOptions align)
        {
            var tmp = Text(name, parent, content, size, color, align, null);
            if (SansFont != null) tmp.font = SansFont;
            if (MatSans != null) tmp.fontSharedMaterial = MatSans;
            return tmp;
        }

        static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            return g != null ? g : go.AddComponent<CanvasGroup>();
        }

        // ========================================================== escena

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Void;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            var urp = go.GetComponent<UniversalAdditionalCameraData>();
            if (urp == null) go.AddComponent<UniversalAdditionalCameraData>();
        }

        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();

            const string actionsPath = "Assets/Settings/InputSystem_Actions.inputactions";
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(actionsPath);
            if (asset == null) return;

            var refs = new Dictionary<string, InputActionReference>();
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(actionsPath))
                if (sub is InputActionReference r && r.action != null && r.action.actionMap != null)
                    refs[$"{r.action.actionMap.name}/{r.action.name}"] = r;

            InputActionReference Find(string key) => refs.TryGetValue(key, out var v) ? v : null;

            try
            {
                module.actionsAsset = asset;
                if (Find("UI/Point") != null) module.point = Find("UI/Point");
                if (Find("UI/Click") != null) module.leftClick = Find("UI/Click");
                if (Find("UI/RightClick") != null) module.rightClick = Find("UI/RightClick");
                if (Find("UI/MiddleClick") != null) module.middleClick = Find("UI/MiddleClick");
                if (Find("UI/ScrollWheel") != null) module.scrollWheel = Find("UI/ScrollWheel");
                if (Find("UI/Navigate") != null) module.move = Find("UI/Navigate");
                if (Find("UI/Submit") != null) module.submit = Find("UI/Submit");
                if (Find("UI/Cancel") != null) module.cancel = Find("UI/Cancel");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Ekkar] No pude enlazar las acciones de UI ({e.Message}). " +
                                 "El modulo usara sus acciones por defecto.");
            }
        }

        static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas_MenuPrincipal", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            canvas.pixelPerfect = false;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            return canvas;
        }

        // ========================================================== fondo

        static CanvasGroup BuildBackground(RectTransform parent, out ParticleField embers,
                                           out ParticleField stars, out EnergyLines lines)
        {
            var root = Rect("00_Fondo", parent);
            Stretch(root);
            var group = Group(root.gameObject);

            // ilustracion principal
            var art = Img("Ilustracion", root, Background, Color.white);
            Stretch(art.rectTransform, -80, -80, -80, -80);
            art.preserveAspect = false;
            art.gameObject.AddComponent<KenBurns>();

            // velo de color que unifica la paleta
            var tint = Img("VeloDeColor", root, Sp("ui_screen_tint"), Color.white);
            Stretch(tint.rectTransform);

            // ---- portal / esfera de reloj detras del personaje
            var clock = Rect("PortalReloj", root);
            Place(clock, ACenter, ACenter, new Vector2(470f, -40f), new Vector2(820f, 820f));
            AddParallax(clock, new Vector2(-16f, -10f));

            var ring = Img("Anillo", clock, Sp("ui_clock_ring"), Palette.Cyan.WithAlpha(0.16f));
            Stretch(ring.rectTransform);
            AddSpin(ring.gameObject, 1.6f);

            var ring2 = Img("AnilloInterior", clock, Sp("ui_clock_ring"), Palette.Gold.WithAlpha(0.10f));
            Place(ring2.rectTransform, ACenter, ACenter, Vector2.zero, new Vector2(560f, 560f));
            AddSpin(ring2.gameObject, -2.6f);

            var handLong = Img("AgujaMinutos", clock, Sp("ui_clock_hand_long"), Palette.CyanBright.WithAlpha(0.20f));
            Place(handLong.rectTransform, ACenter, ACenter, Vector2.zero, new Vector2(28f, 700f));
            AddSpin(handLong.gameObject, -14f, false);

            var handShort = Img("AgujaHoras", clock, Sp("ui_clock_hand_short"), Palette.Gold.WithAlpha(0.24f));
            Place(handShort.rectTransform, ACenter, ACenter, Vector2.zero, new Vector2(28f, 560f));
            AddSpin(handShort.gameObject, -1.2f, false);

            // ---- engranajes flotantes en dos planos
            var gearsFar = Rect("EngranajesLejanos", root);
            Stretch(gearsFar);
            AddParallax(gearsFar, new Vector2(-10f, -6f));
            Gear(gearsFar, "Engranaje_1", "ui_gear_a", new Vector2(-770f, 350f), 150f, Palette.Gold.WithAlpha(0.12f), 7f);
            Gear(gearsFar, "Engranaje_2", "ui_gear_c", new Vector2(-330f, 430f), 74f, Palette.Cyan.WithAlpha(0.11f), -13f);
            Gear(gearsFar, "Engranaje_3", "ui_gear_b", new Vector2(760f, 400f), 108f, Palette.Gold.WithAlpha(0.10f), 9f);

            var gearsNear = Rect("EngranajesCercanos", root);
            Stretch(gearsNear);
            AddParallax(gearsNear, new Vector2(-26f, -16f));
            Gear(gearsNear, "Engranaje_4", "ui_gear_a", new Vector2(-660f, -330f), 205f, Palette.Gold.WithAlpha(0.10f), -5f);
            Gear(gearsNear, "Engranaje_5", "ui_gear_b", new Vector2(330f, -420f), 96f, Palette.PurpleLight.WithAlpha(0.13f), 12f);

            // ---- estrellas, lineas de energia y particulas
            var starsRt = Rect("Estrellas", root);
            Stretch(starsRt);
            stars = starsRt.gameObject.AddComponent<ParticleField>();
            ConfigureField(stars, ParticleField.FieldMode.Stars, Sp("ui_dot"), 70,
                           new Vector2(2f, 5f), 0.55f);
            AddParallax(starsRt, new Vector2(-8f, -5f));

            var linesRt = Rect("LineasDeEnergia", root);
            Stretch(linesRt);
            lines = linesRt.gameObject.AddComponent<EnergyLines>();
            SetPrivate(lines, "lineSprite", Sp("ui_gradient_h"));

            var embersRt = Rect("ParticulasTemporales", root);
            Stretch(embersRt);
            embers = embersRt.gameObject.AddComponent<ParticleField>();
            ConfigureField(embers, ParticleField.FieldMode.Embers, Sp("ui_spark"), 60,
                           new Vector2(4f, 10f), 0.6f);
            AddParallax(embersRt, new Vector2(-18f, -12f));

            // ---- capas de acabado
            var vignette = Img("Vineta", root, Sp("ui_vignette"), Color.white);
            Stretch(vignette.rectTransform, -140, -100, -140, -100);

            var scanGo = new GameObject("LineasDeBarrido", typeof(RectTransform), typeof(RawImage));
            var scanRt = (RectTransform)scanGo.transform;
            scanRt.SetParent(root, false);
            Stretch(scanRt);
            var raw = scanGo.GetComponent<RawImage>();
            var scanSprite = Sp("ui_scanlines");
            raw.texture = scanSprite != null ? scanSprite.texture : null;
            raw.color = new Color(1f, 1f, 1f, 0.5f);
            raw.raycastTarget = false;
            scanGo.AddComponent<ScrollingTexture>();

            return group;
        }

        static void ConfigureField(ParticleField field, ParticleField.FieldMode mode, Sprite sprite,
                                   int count, Vector2 sizeRange, float maxAlpha)
        {
            SetPrivate(field, "mode", mode);
            SetPrivate(field, "particleSprite", sprite);
            SetPrivate(field, "count", count);
            SetPrivate(field, "sizeRange", sizeRange);
            SetPrivate(field, "maxAlpha", maxAlpha);
            SetPrivate(field, "tints", new[] { Palette.Cyan, Palette.CyanBright, Palette.Gold, Palette.PurpleLight });
        }

        static void Gear(Transform parent, string name, string sprite, Vector2 pos, float size, Color color, float speed)
        {
            var img = Img(name, parent, Sp(sprite), color);
            Place(img.rectTransform, ACenter, ACenter, pos, new Vector2(size, size));
            AddSpin(img.gameObject, speed);
        }

        static void AddSpin(GameObject go, float speed, bool randomAngle = true)
        {
            var spin = go.AddComponent<UISpin>();
            SetPrivate(spin, "degreesPerSecond", speed);
            SetPrivate(spin, "randomizeStartAngle", randomAngle);
        }

        static void AddParallax(RectTransform rt, Vector2 strength)
        {
            var p = rt.gameObject.AddComponent<UIParallax>();
            SetPrivate(p, "strength", strength);
        }

        // ======================================================= personaje

        static CanvasGroup BuildCharacter(RectTransform parent)
        {
            var root = Rect("01_Personaje", parent);
            Stretch(root);
            var group = Group(root.gameObject);

            var aura = Img("Aura", root, Sp("ui_glow"), Palette.Cyan.WithAlpha(0.20f));
            Place(aura.rectTransform, ABottom, ACenter, new Vector2(470f, 460f), new Vector2(900f, 900f));
            var pulse = aura.gameObject.AddComponent<UIPulse>();
            SetPrivate(pulse, "target", aura);
            SetPrivate(pulse, "minAlpha", 0.10f);
            SetPrivate(pulse, "maxAlpha", 0.26f);
            SetPrivate(pulse, "period", 5.5f);
            SetPrivate(pulse, "scaleAmount", 0.04f);
            SetPrivate(pulse, "startDelay", 2.5f);

            var echoRoot = Rect("EcosTemporales", root);
            Stretch(echoRoot);

            var ekkar = Img("Ekkar", root, EkkarFrames.Length > 0 ? EkkarFrames[0] : null, Color.white);
            Place(ekkar.rectTransform, ABottom, ABottom, new Vector2(470f, 120f), new Vector2(400f, 618f));
            ekkar.preserveAspect = true;

            if (EkkarFrames.Length > 1)
            {
                var seq = ekkar.gameObject.AddComponent<SpriteSequence>();
                SetPrivate(seq, "frames", EkkarFrames);
                SetPrivate(seq, "framesPerSecond", 10f);
            }

            var floatFx = ekkar.gameObject.AddComponent<UIFloat>();
            SetPrivate(floatFx, "amplitude", new Vector2(0f, 5f));
            SetPrivate(floatFx, "period", new Vector2(0f, 6.5f));
            SetPrivate(floatFx, "startDelay", 2.6f);

            var echo = echoRoot.gameObject.AddComponent<TemporalEcho>();
            SetPrivate(echo, "source", ekkar);
            SetPrivate(echo, "echoCount", 3);
            SetPrivate(echo, "delayPerEcho", 0.14f);
            SetPrivate(echo, "offsetPerEcho", new Vector2(-30f, 4f));
            SetPrivate(echo, "firstEchoAlpha", 0.22f);
            SetPrivate(echo, "tint", Palette.CyanBright);

            return group;
        }

        // =========================================================== menu

        struct MenuRefs
        {
            public CanvasGroup group;
        }

        static MenuRefs BuildMenu(RectTransform parent,
                                  out MenuButton play, out MenuButton options,
                                  out MenuButton credits, out MenuButton quit,
                                  out CanvasGroup titleGroup, out CanvasGroup dividerGroup,
                                  out TMPTextIntro[] titleIntros)
        {
            var root = Rect("02_Menu", parent);
            Stretch(root);
            var group = Group(root.gameObject);

            const float x = 150f;

            // ---------------------------------------------------- titulo
            var title = Rect("Titulo", root);
            Place(title, ALeft, ALeft, new Vector2(x, 250f), new Vector2(1000f, 460f));
            titleGroup = Group(title.gameObject);

            var scrim = Img("VeloDeLectura", title, Sp("ui_glow"), Palette.Void.WithAlpha(0.55f));
            Place(scrim.rectTransform, ALeft, ALeft, new Vector2(-160f, -20f), new Vector2(1250f, 620f));

            var subtitle = Text("Subtitulo", title, "UNA AVENTURA A TRAVES DEL TIEMPO", 21f,
                                Palette.CyanBright, TextAlignmentOptions.Left, MatAccent);
            Place(subtitle.rectTransform, ALeft, ALeft, new Vector2(4f, 168f), new Vector2(900f, 30f));
            subtitle.characterSpacing = 12f;
            var subPulse = subtitle.gameObject.AddComponent<UIPulse>();
            SetPrivate(subPulse, "target", subtitle);
            SetPrivate(subPulse, "minAlpha", 0.55f);
            SetPrivate(subPulse, "maxAlpha", 1f);
            SetPrivate(subPulse, "period", 3.2f);
            SetPrivate(subPulse, "startDelay", 2.4f);

            var line1 = Text("Titulo_Linea1", title, "GUERRERO", 104f, Palette.GoldLight, TextAlignmentOptions.Left, MatTitle);
            Place(line1.rectTransform, ALeft, ALeft, new Vector2(0f, 86f), new Vector2(1000f, 120f));
            ApplyTitleGradient(line1);

            var line2 = Text("Titulo_Linea2", title, "DEL TIEMPO", 104f, Palette.GoldLight, TextAlignmentOptions.Left, MatTitle);
            Place(line2.rectTransform, ALeft, ALeft, new Vector2(0f, -12f), new Vector2(1000f, 120f));
            ApplyTitleGradient(line2);

            var intro1 = line1.gameObject.AddComponent<TMPTextIntro>();
            SetPrivate(intro1, "startDelay", 0.35f);
            SetPrivate(intro1, "charDelay", 0.05f);
            SetPrivate(intro1, "rotationJitter", 12f);

            var intro2 = line2.gameObject.AddComponent<TMPTextIntro>();
            SetPrivate(intro2, "startDelay", 0.62f);
            SetPrivate(intro2, "charDelay", 0.05f);
            SetPrivate(intro2, "rotationJitter", 12f);
            titleIntros = new[] { intro1, intro2 };

            var glow1 = line1.gameObject.AddComponent<TMPGlowPulse>();
            SetPrivate(glow1, "glowColor", Palette.Gold);
            SetPrivate(glow1, "startDelay", 1.6f);
            var glow2 = line2.gameObject.AddComponent<TMPGlowPulse>();
            SetPrivate(glow2, "glowColor", Palette.Gold);
            SetPrivate(glow2, "startDelay", 1.8f);

            var tagline = TextSans("Lema", title, "El destino de todas las eras está en tus manos", 25f,
                                   Palette.TextDim, TextAlignmentOptions.Left);
            Place(tagline.rectTransform, ALeft, ALeft, new Vector2(6f, -90f), new Vector2(900f, 34f));
            tagline.characterSpacing = 2f;

            // -------------------------------------------------- separador
            var divider = Rect("Separador", root);
            Place(divider, ALeft, ALeft, new Vector2(x, 40f), new Vector2(470f, 24f));
            dividerGroup = Group(divider.gameObject);

            var lineL = Img("LineaIzquierda", divider, Sp("ui_gradient_h"), Palette.Cyan.WithAlpha(0.55f));
            Place(lineL.rectTransform, ALeft, ALeft, new Vector2(0f, 0f), new Vector2(190f, 2f));

            var crystal = Img("Cristal", divider, Sp("ui_diamond"), Palette.CyanBright);
            Place(crystal.rectTransform, ALeft, ACenter, new Vector2(214f, 0f), new Vector2(22f, 22f));
            var crystalFloat = crystal.gameObject.AddComponent<UIFloat>();
            SetPrivate(crystalFloat, "amplitude", new Vector2(0f, 5f));
            SetPrivate(crystalFloat, "period", new Vector2(0f, 2.4f));
            SetPrivate(crystalFloat, "rotationAmplitude", 18f);
            SetPrivate(crystalFloat, "rotationPeriod", 5f);
            SetPrivate(crystalFloat, "startDelay", 2.4f);

            var lineR = Img("LineaDerecha", divider, Sp("ui_gradient_h"), Palette.Cyan.WithAlpha(0.55f));
            Place(lineR.rectTransform, ALeft, ALeft, new Vector2(240f, 0f), new Vector2(190f, 2f));

            // ---------------------------------------------------- botones
            var buttons = Rect("Botones", root);
            Place(buttons, ALeft, ALeft, new Vector2(x, -170f), new Vector2(480f, 380f));

            play = MakeButton(buttons, "Btn_Jugar", "JUGAR", "ui_arrow_right",
                              new Vector2(0f, 148f), new Vector2(470f, 86f), 30f, true);
            options = MakeButton(buttons, "Btn_Opciones", "OPCIONES", "ui_gear_c",
                                 new Vector2(0f, 52f), new Vector2(470f, 76f), 26f, false);
            credits = MakeButton(buttons, "Btn_Creditos", "CREDITOS", "ui_diamond",
                                 new Vector2(0f, -34f), new Vector2(470f, 76f), 26f, false);
            quit = MakeButton(buttons, "Btn_Salir", "SALIR", "ui_arrow_left",
                              new Vector2(0f, -120f), new Vector2(470f, 76f), 26f, false);

            SetNavigation(play, quit, options);
            SetNavigation(options, play, credits);
            SetNavigation(credits, options, quit);
            SetNavigation(quit, credits, play);

            return new MenuRefs { group = group };
        }

        static void ApplyTitleGradient(TextMeshProUGUI text)
        {
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(
                Palette.GoldLight, Palette.GoldLight, Palette.Gold, Palette.CyanBright);
            text.characterSpacing = 2f;
        }

        static void SetNavigation(MenuButton button, MenuButton up, MenuButton down)
        {
            var nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down
            };
            button.navigation = nav;
        }

        static MenuButton MakeButton(Transform parent, string name, string label, string iconSprite,
                                     Vector2 pos, Vector2 size, float fontSize, bool highlighted)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Place(rt, ALeft, ALeft, pos, size);

            var hit = go.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var button = go.AddComponent<MenuButton>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            var fill = Img("Relleno", rt, Sp("ui_fill"), CFillIdle, Image.Type.Sliced);
            Stretch(fill.rectTransform);

            // sombreado inferior: evita que el boton se vea como una plancha plana
            var shade = Img("Sombreado", rt, Sp("ui_gradient_v"), new Color(0f, 0f, 0f, 0.28f));
            Stretch(shade.rectTransform, 2, 2, 2, 2);

            var frame = Img("Marco", rt, Sp("ui_frame"), CFrameIdle, Image.Type.Sliced);
            Stretch(frame.rectTransform);

            Image brackets = null;
            if (highlighted)
            {
                brackets = Img("Escuadras", rt, Sp("ui_brackets"), Palette.Gold.WithAlpha(0.9f), Image.Type.Sliced);
                Stretch(brackets.rectTransform, -5, -5, -5, -5);
            }

            var maskRt = Rect("MascaraDeBrillo", rt);
            Stretch(maskRt);
            maskRt.gameObject.AddComponent<RectMask2D>();

            var shine = Img("Brillo", maskRt, Sp("ui_gradient_h"), Palette.CyanBright.WithAlpha(0f));
            Place(shine.rectTransform, ACenter, ACenter, Vector2.zero, new Vector2(220f, size.y));

            var accent = Img("Acento", rt, Sp("ui_solid"), CFrameIdle);
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.sizeDelta = new Vector2(5f, -16f);
            accent.rectTransform.anchoredPosition = new Vector2(3f, 0f);

            var icon = Img("Icono", rt, Sp(iconSprite), Palette.Armor);
            Place(icon.rectTransform, ALeft, ACenter, new Vector2(38f, 0f), new Vector2(22f, 22f));

            var text = Text("Etiqueta", rt, label, fontSize, Palette.Armor, TextAlignmentOptions.Left, MatBody);
            Place(text.rectTransform, ALeft, ALeft, new Vector2(72f, 0f), new Vector2(size.x - 110f, size.y));
            text.characterSpacing = 4f;

            var arrow = Img("Flecha", rt, Sp("ui_arrow_right"), Palette.CyanBright.WithAlpha(0f));
            Place(arrow.rectTransform, ARight, ACenter, new Vector2(-28f, 0f), new Vector2(18f, 18f));

            button.fill = fill;
            button.frame = frame;
            button.accent = accent;
            button.shine = shine;
            button.arrow = arrow;
            button.icon = icon;
            button.label = text;

            button.fillIdle = CFillIdle;
            button.frameIdle = highlighted ? Palette.Gold.WithAlpha(0.9f) : CFrameIdle;
            button.accentIdle = highlighted ? Palette.Gold.WithAlpha(0.85f) : CFrameIdle;
            button.labelIdle = highlighted ? Palette.GoldLight : Palette.Armor;

            button.ApplyFocusColors(highlighted);
            return button;
        }

        // ========================================================== pie

        static CanvasGroup BuildFooter(RectTransform parent)
        {
            var root = Rect("03_PieDePagina", parent);
            Stretch(root);
            var group = Group(root.gameObject);

            var controls = Rect("Controles", root);
            Place(controls, ABottomLeft, ABottomLeft, new Vector2(150f, 46f), new Vector2(1200f, 130f));

            // las tres filas son la lista entera de teclas, la misma que sale
            // en el menu de pausa: faltaban el ataque alto, el golpe cargado,
            // envainar y la propia tecla de pausa
            float y3 = 94f, y2 = 56f, y1 = 18f;
            float cx = 0f;
            cx = KeyGroup(controls, cx, y3, new[] { "A", "D" }, "MOVER");
            cx = KeyGroup(controls, cx, y3, new[] { "ESPACIO" }, "SALTAR   x2 DOBLE");
            KeyGroup(controls, cx, y3, new[] { "SHIFT" }, "DASH");

            cx = 0f;
            cx = KeyGroup(controls, cx, y2, new[] { "J" }, "ATACAR");
            cx = KeyGroup(controls, cx, y2, new[] { "I", "J" }, "ATAQUE ALTO");
            KeyGroup(controls, cx, y2, new[] { "K" }, "GOLPE CARGADO");

            cx = 0f;
            cx = KeyGroup(controls, cx, y1, new[] { "E" }, "DETENER TIEMPO");
            cx = KeyGroup(controls, cx, y1, new[] { "R" }, "CHRONOBREAK");
            cx = KeyGroup(controls, cx, y1, new[] { "Q" }, "ENVAINAR");
            KeyGroup(controls, cx, y1, new[] { "ESC" }, "PAUSA");

            var version = Text("Version", root, "UNITY 6.5   -   v0.1.0 ALPHA", 16f,
                               Palette.TextDim.WithAlpha(0.5f), TextAlignmentOptions.Right, MatBody);
            Place(version.rectTransform, ABottomRight, ABottomRight, new Vector2(-150f, 48f), new Vector2(520f, 24f));

            return group;
        }

        static float KeyGroup(Transform parent, float x, float y, string[] keys, string label)
        {
            foreach (var key in keys)
            {
                float w = Mathf.Max(28f, 16f + key.Length * 11f);
                var cap = Img($"Tecla_{key}", parent, Sp("ui_keycap"),
                              Palette.PurpleLight.WithAlpha(0.55f), Image.Type.Sliced);
                Place(cap.rectTransform, ABottomLeft, ABottomLeft, new Vector2(x, y), new Vector2(w, 26f));

                var t = Text("Letra", cap.transform, key, 14f, Palette.PurpleLight, TextAlignmentOptions.Center, MatBody);
                Stretch(t.rectTransform, 0, 1, 0, 0);

                x += w + 4f;
            }

            x += 6f;
            var lab = Text($"Accion_{label}", parent, label, 14f, Palette.TextDim.WithAlpha(0.62f),
                           TextAlignmentOptions.Left, MatBody);
            Place(lab.rectTransform, ABottomLeft, ABottomLeft, new Vector2(x, y + 4f), new Vector2(240f, 20f));
            lab.characterSpacing = 3f;
            lab.ForceMeshUpdate();

            return x + Mathf.Max(lab.preferredWidth, label.Length * 10f) + 30f;
        }

        // ======================================================= paneles

        static RectTransform PanelShell(Transform parent, string name, Vector2 boxSize, string title,
                                        out UIPanel panel, out RectTransform box, MainMenuController controller)
        {
            var root = Rect(name, parent);
            Stretch(root);
            var group = Group(root.gameObject);

            var scrim = Img("Velo", root, Sp("ui_solid"), Palette.Void.WithAlpha(0f), Image.Type.Simple, true);
            Stretch(scrim.rectTransform);

            box = Rect("Caja", root);
            Place(box, ACenter, ACenter, Vector2.zero, boxSize);

            var fill = Img("Fondo", box, Sp("ui_panel_fill"), Palette.PurpleDeep.WithAlpha(0.97f), Image.Type.Sliced, true);
            Stretch(fill.rectTransform);

            var glowEdge = Img("Resplandor", box, Sp("ui_glow"), Palette.PurpleLight.WithAlpha(0.16f));
            Stretch(glowEdge.rectTransform, -70, -70, -70, -70);

            var frame = Img("Marco", box, Sp("ui_panel_frame"), Palette.PurpleLight, Image.Type.Sliced);
            Stretch(frame.rectTransform);

            var header = Text("Titulo", box, title, 32f, Palette.GoldLight, TextAlignmentOptions.Center, MatTitle);
            Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(boxSize.x - 80f, 44f));
            header.characterSpacing = 6f;

            var underline = Img("SubrayadoTitulo", box, Sp("ui_gradient_h"), Palette.Gold.WithAlpha(0.7f));
            Place(underline.rectTransform, new Vector2(0.5f, 1f), ACenter, new Vector2(0f, -80f), new Vector2(boxSize.x - 140f, 2f));

            panel = root.gameObject.AddComponent<UIPanel>();
            SetPrivate(panel, "group", group);
            SetPrivate(panel, "box", box);
            SetPrivate(panel, "scrim", scrim);

            var closer = scrim.gameObject.AddComponent<ScrimCloser>();
            SetPrivate(closer, "panel", panel);

            return root;
        }

        static UIPanel BuildOptionsPanel(Transform parent, MainMenuController controller, out OptionsPanel logic)
        {
            var boxSize = new Vector2(760f, 740f);
            var root = PanelShell(parent, "Panel_Opciones", boxSize, "OPCIONES", out UIPanel panel, out RectTransform box, controller);
            logic = root.gameObject.AddComponent<OptionsPanel>();
            SetPrivate(logic, "panel", panel);

            var music = SliderRow(box, "Fila_Musica", "MUSICA", new Vector2(0f, 250f));
            var sfx = SliderRow(box, "Fila_Efectos", "EFECTOS DE SONIDO", new Vector2(0f, 168f));
            var res = SelectorRow(box, "Fila_Resolucion", "RESOLUCION", new Vector2(0f, 80f));
            var mode = SelectorRow(box, "Fila_ModoPantalla", "MODO DE PANTALLA", new Vector2(0f, 6f));
            var vsync = ToggleRow(box, "Fila_VSync", "SINCRONIZACION VERTICAL", new Vector2(0f, -62f));
            var particles = ToggleRow(box, "Fila_Particulas", "EFECTOS DE PARTICULAS", new Vector2(0f, -118f));

            var notice = Text("Aviso_Guardado", box, "AJUSTES GUARDADOS", 20f,
                              Palette.CyanBright, TextAlignmentOptions.Center, MatAccent);
            Place(notice.rectTransform, ACenter, ACenter, new Vector2(0f, -196f), new Vector2(600f, 30f));
            var noticeGroup = Group(notice.gameObject);
            noticeGroup.alpha = 0f;

            var reset = MakeSmallButton(box, "Btn_Restablecer", "REINICIAR", new Vector2(-232f, -282f), new Vector2(220f, 56f), false);
            var cancel = MakeSmallButton(box, "Btn_Cancelar", "CANCELAR", new Vector2(0f, -282f), new Vector2(220f, 56f), false);
            var save = MakeSmallButton(box, "Btn_Guardar", "GUARDAR", new Vector2(232f, -282f), new Vector2(220f, 56f), true);

            SetPrivate(logic, "musicSlider", music);
            SetPrivate(logic, "sfxSlider", sfx);
            SetPrivate(logic, "resolutionSelector", res);
            SetPrivate(logic, "screenModeSelector", mode);
            SetPrivate(logic, "vsyncToggle", vsync);
            SetPrivate(logic, "particlesToggle", particles);
            SetPrivate(logic, "savedNotice", noticeGroup);

            UnityEventTools.AddPersistentListener(reset.onClick, logic.Restablecer);
            UnityEventTools.AddPersistentListener(cancel.onClick, logic.Cancelar);
            UnityEventTools.AddPersistentListener(save.onClick, logic.Guardar);

            SetPrivate(panel, "firstSelected", music.Slider);

            // navegacion vertical del panel
            LinkVertical(new Selectable[] { music.Slider, sfx.Slider, res, mode, vsync, particles, save });
            return panel;
        }

        static LabeledSlider SliderRow(Transform parent, string name, string label, Vector2 pos)
        {
            var row = Rect(name, parent);
            Place(row, ACenter, ACenter, pos, new Vector2(640f, 74f));

            var lab = Text("Etiqueta", row, label, 19f, Palette.Cyan, TextAlignmentOptions.Left, MatBody);
            Place(lab.rectTransform, ALeft, ALeft, new Vector2(0f, 22f), new Vector2(440f, 26f));
            lab.characterSpacing = 4f;

            var value = Text("Valor", row, "100%", 19f, Palette.Armor, TextAlignmentOptions.Right, MatBody);
            Place(value.rectTransform, ARight, ARight, new Vector2(0f, 22f), new Vector2(140f, 26f));

            // ---- slider
            var sliderGo = new GameObject("Deslizador", typeof(RectTransform), typeof(Slider));
            var sliderRt = (RectTransform)sliderGo.transform;
            sliderRt.SetParent(row, false);
            Place(sliderRt, ACenter, ACenter, new Vector2(0f, -16f), new Vector2(640f, 24f));

            var track = Img("Fondo", sliderRt, Sp("ui_slider_track"), Palette.PurpleDeep.WithAlpha(0.95f), Image.Type.Sliced);
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.pivot = ACenter;
            track.rectTransform.sizeDelta = new Vector2(0f, 14f);
            track.rectTransform.anchoredPosition = Vector2.zero;

            var fillArea = Rect("Zona de relleno", sliderRt);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.pivot = ACenter;
            fillArea.sizeDelta = new Vector2(-18f, 14f);
            fillArea.anchoredPosition = Vector2.zero;

            var fill = Img("Relleno", fillArea, Sp("ui_slider_fill"), Palette.Cyan, Image.Type.Sliced);
            fill.rectTransform.sizeDelta = Vector2.zero;
            fill.rectTransform.anchoredPosition = Vector2.zero;

            var handleArea = Rect("Zona del mango", sliderRt);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.pivot = ACenter;
            handleArea.offsetMin = new Vector2(9f, 0f);
            handleArea.offsetMax = new Vector2(-9f, 0f);

            // El color base va en blanco porque la transicion ColorTint
            // multiplica: si aqui pusieramos oro, el estado "seleccionado"
            // (cian) daria verde.
            var handle = Img("Mango", handleArea, Sp("ui_slider_knob"), Color.white, Image.Type.Simple, true);
            handle.rectTransform.sizeDelta = new Vector2(18f, 30f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 1f;
            slider.transition = Selectable.Transition.ColorTint;
            var colors = slider.colors;
            colors.colorMultiplier = 1f;
            colors.normalColor = Palette.Gold;
            colors.highlightedColor = Palette.GoldLight;
            colors.selectedColor = Palette.CyanBright;
            colors.pressedColor = Color.white;
            colors.disabledColor = Palette.TextDim;
            colors.fadeDuration = 0.12f;
            slider.colors = colors;

            var labeled = row.gameObject.AddComponent<LabeledSlider>();
            SetPrivate(labeled, "slider", slider);
            SetPrivate(labeled, "valueLabel", value);
            return labeled;
        }

        static OptionSelector SelectorRow(Transform parent, string name, string label, Vector2 pos)
        {
            var row = Rect(name, parent);
            Place(row, ACenter, ACenter, pos, new Vector2(640f, 58f));

            var lab = Text("Etiqueta", row, label, 19f, Palette.Cyan, TextAlignmentOptions.Left, MatBody);
            Place(lab.rectTransform, ALeft, ALeft, new Vector2(0f, 0f), new Vector2(300f, 26f));
            lab.characterSpacing = 4f;

            var widgetGo = new GameObject("Selector", typeof(RectTransform), typeof(Image));
            var widget = (RectTransform)widgetGo.transform;
            widget.SetParent(row, false);
            Place(widget, ARight, ARight, new Vector2(0f, 0f), new Vector2(330f, 46f));

            var hit = widgetGo.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var selector = widgetGo.AddComponent<OptionSelector>();
            selector.targetGraphic = hit;
            selector.transition = Selectable.Transition.None;

            var bg = Img("Fondo", widget, Sp("ui_fill"), Palette.Void.WithAlpha(0.75f), Image.Type.Sliced);
            Stretch(bg.rectTransform);

            var frame = Img("Marco", widget, Sp("ui_frame"), Palette.PurpleLight.WithAlpha(0.5f), Image.Type.Sliced);
            Stretch(frame.rectTransform);

            var value = Text("Valor", widget, "-", 20f, Palette.Armor, TextAlignmentOptions.Center, MatBody);
            Place(value.rectTransform, ACenter, ACenter, Vector2.zero, new Vector2(230f, 30f));

            var prev = ArrowButton(widget, "Btn_Anterior", "ui_arrow_left", new Vector2(20f, 0f), ALeft);
            var next = ArrowButton(widget, "Btn_Siguiente", "ui_arrow_right", new Vector2(-20f, 0f), ARight);

            SetPrivate(selector, "valueLabel", value);
            SetPrivate(selector, "prevButton", prev);
            SetPrivate(selector, "nextButton", next);
            SetPrivate(selector, "frame", frame);
            SetPrivate(selector, "frameIdle", Palette.PurpleLight.WithAlpha(0.5f));
            SetPrivate(selector, "frameActive", Palette.CyanBright);
            return selector;
        }

        static Button ArrowButton(Transform parent, string name, string sprite, Vector2 pos, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Place(rt, anchor, ACenter, pos, new Vector2(30f, 34f));

            var img = go.GetComponent<Image>();
            img.sprite = Sp(sprite);
            img.color = Color.white;   // el tinte real lo pone el ColorBlock
            img.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.colorMultiplier = 1f;
            colors.normalColor = Palette.TextDim;
            colors.highlightedColor = Palette.CyanBright;
            colors.pressedColor = Palette.Gold;
            colors.selectedColor = Palette.CyanBright;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        static PixelToggle ToggleRow(Transform parent, string name, string label, Vector2 pos)
        {
            var row = Rect(name, parent);
            Place(row, ACenter, ACenter, pos, new Vector2(640f, 46f));

            var lab = Text("Etiqueta", row, label, 19f, Palette.Cyan, TextAlignmentOptions.Left, MatBody);
            Place(lab.rectTransform, ALeft, ALeft, new Vector2(0f, 0f), new Vector2(420f, 26f));
            lab.characterSpacing = 4f;

            var go = new GameObject("Interruptor", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(row, false);
            Place(rt, ARight, ARight, new Vector2(0f, 0f), new Vector2(76f, 38f));

            var hit = go.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var toggle = go.AddComponent<PixelToggle>();
            toggle.targetGraphic = hit;
            toggle.transition = Selectable.Transition.None;

            var track = Img("Pista", rt, Sp("ui_toggle_track"), Palette.PurpleDeep, Image.Type.Sliced);
            Place(track.rectTransform, ACenter, ACenter, Vector2.zero, new Vector2(68f, 32f));

            var focus = Img("Foco", rt, Sp("ui_toggle_track"), Palette.CyanBright.WithAlpha(0f), Image.Type.Sliced);
            Place(focus.rectTransform, ACenter, ACenter, Vector2.zero, new Vector2(76f, 40f));

            var knob = Img("Pastilla", rt, Sp("ui_toggle_knob"), Palette.Gold);
            Place(knob.rectTransform, ACenter, ACenter, new Vector2(17f, 0f), new Vector2(24f, 24f));

            SetPrivate(toggle, "track", track);
            SetPrivate(toggle, "knobImage", knob);
            SetPrivate(toggle, "knob", knob.rectTransform);
            SetPrivate(toggle, "focusFrame", focus);
            SetPrivate(toggle, "trackOff", Palette.PurpleDeep);
            SetPrivate(toggle, "trackOn", Palette.Cyan.WithAlpha(0.85f));
            SetPrivate(toggle, "knobOff", Palette.TextDim);
            SetPrivate(toggle, "knobOn", Palette.GoldLight);
            SetPrivate(toggle, "knobOffX", -17f);
            SetPrivate(toggle, "knobOnX", 17f);
            return toggle;
        }

        static UIPanel BuildCreditsPanel(Transform parent, MainMenuController controller)
        {
            var boxSize = new Vector2(820f, 860f);
            var root = PanelShell(parent, "Panel_Creditos", boxSize, "CREDITOS", out UIPanel panel, out RectTransform box, controller);
            var logic = root.gameObject.AddComponent<CreditsPanel>();
            SetPrivate(logic, "panel", panel);

            // ---- scroll
            var scrollGo = new GameObject("Desplazamiento", typeof(RectTransform), typeof(ScrollRect));
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.SetParent(box, false);
            Place(scrollRt, ACenter, ACenter, new Vector2(0f, -10f), new Vector2(700f, 600f));

            var viewport = Rect("Ventana", scrollRt);
            Stretch(viewport, 0, 0, 24, 0);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = Rect("Contenido", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 0f);
            content.anchoredPosition = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 6, 40);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildCreditsContent(content);

            // ---- barra
            var barGo = new GameObject("Barra", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var barRt = (RectTransform)barGo.transform;
            barRt.SetParent(scrollRt, false);
            barRt.anchorMin = new Vector2(1f, 0f);
            barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot = new Vector2(1f, 0.5f);
            barRt.sizeDelta = new Vector2(10f, 0f);
            barRt.anchoredPosition = Vector2.zero;

            var barBg = barGo.GetComponent<Image>();
            barBg.sprite = Sp("ui_scroll_handle");
            barBg.type = Image.Type.Sliced;
            barBg.pixelsPerUnitMultiplier = 1f;
            barBg.color = Palette.Void.WithAlpha(0.6f);

            var slideArea = Rect("Zona", barRt);
            Stretch(slideArea);

            var handle = Img("Mango", slideArea, Sp("ui_scroll_handle"), Color.white, Image.Type.Sliced, true);
            // El Scrollbar recalcula los anclajes; el tamano debe quedar a cero
            // para que el mango ocupe exactamente la franja calculada.
            handle.rectTransform.anchorMin = Vector2.zero;
            handle.rectTransform.anchorMax = Vector2.one;
            handle.rectTransform.pivot = ACenter;
            handle.rectTransform.sizeDelta = Vector2.zero;
            handle.rectTransform.anchoredPosition = Vector2.zero;

            var scrollbar = barGo.GetComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var sbColors = scrollbar.colors;
            sbColors.colorMultiplier = 1f;
            sbColors.normalColor = Palette.PurpleLight;
            sbColors.highlightedColor = Palette.CyanBright;
            sbColors.pressedColor = Palette.Gold;
            scrollbar.colors = sbColors;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 34f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            SetPrivate(logic, "scroll", scroll);
            SetPrivate(logic, "hoverArea", viewport);

            var close = MakeSmallButton(box, "Btn_Cerrar", "CERRAR", new Vector2(0f, -370f), new Vector2(280f, 56f), true);
            UnityEventTools.AddPersistentListener(close.onClick, controller.CerrarPanelActivo);
            SetPrivate(panel, "firstSelected", close);

            return panel;
        }

        static void BuildCreditsContent(RectTransform content)
        {
            void Role(string text)
            {
                var t = Text("Rol", content, text, 18f, Palette.Gold, TextAlignmentOptions.Center, MatBody);
                t.characterSpacing = 6f;
                t.margin = new Vector4(0f, 18f, 0f, 2f);
                t.textWrappingMode = TextWrappingModes.Normal;
            }
            void Name(string text)
            {
                var t = TextSans("Nombre", content, text, 25f, Palette.Armor, TextAlignmentOptions.Center);
                t.textWrappingMode = TextWrappingModes.Normal;
                t.fontStyle = FontStyles.Bold;
            }
            void Detail(string text)
            {
                var t = TextSans("Detalle", content, text, 19f, Palette.TextDim, TextAlignmentOptions.Center);
                t.textWrappingMode = TextWrappingModes.Normal;
            }
            void Sep()
            {
                // contenedor de alto fijo con una linea fina centrada dentro
                var holder = Rect("Separador", content);
                var le = holder.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 26f;

                var img = Img("Linea", holder, Sp("ui_gradient_h"), Palette.PurpleLight.WithAlpha(0.8f));
                Place(img.rectTransform, ACenter, ACenter, Vector2.zero, new Vector2(300f, 2f));
            }

            Role("DIRECCION Y DESARROLLO");
            Name("Jesús Alexander Zabala Torres");
            Detail("Programación de Videojuegos — SENA 2026");
            Sep();

            Role("ARTE DEL PERSONAJE");
            Name("Ekkar, el Guerrero del Tiempo");
            Detail("Sprite sheets y animaciones originales");
            Sep();

            Role("ARTE DE ESCENARIOS");
            Name("Gothicvania Cemetery Pack");
            Detail("Edad Media — enemigos y tileset");
            Name("Free Industrial Zone Tileset");
            Detail("Era Industrial — tileset y objetos");
            Name("Cyberpunk City 2");
            Detail("Futuro Digital — tileset, enemigos y música");
            Name("Free Drones Pack Pixel Art");
            Detail("Drones industriales");
            Sep();

            Role("TIPOGRAFIA");
            Name("Perfect DOS VGA 437");
            Detail("Zeh Fernando — fuente pixel de uso libre");
            Name("Liberation Sans");
            Detail("Incluida con TextMeshPro");
            Sep();

            Role("MUSICA Y SONIDO DEL MENU");
            Name("Síntesis por código");
            Detail("Ambiente y efectos generados en tiempo real");
            Sep();

            Role("MOTOR");
            Name("Unity 6.5 (6000.5.5f1)");
            Detail("Universal Render Pipeline 2D");
            Sep();

            Role("GRACIAS POR JUGAR");
            Detail("El destino de todas las eras está en tus manos.");
        }

        static UIPanel BuildQuitPanel(Transform parent, MainMenuController controller)
        {
            var boxSize = new Vector2(720f, 340f);
            var root = PanelShell(parent, "Panel_Salir", boxSize, "ABANDONAR", out UIPanel panel, out RectTransform box, controller);

            var message = TextSans("Mensaje", box, "¿Seguro que quieres abandonar la línea temporal?", 24f,
                                   Palette.Armor, TextAlignmentOptions.Center);
            Place(message.rectTransform, ACenter, ACenter, new Vector2(0f, 22f), new Vector2(620f, 60f));
            message.textWrappingMode = TextWrappingModes.Normal;

            var hint = TextSans("Aviso", box, "Se perderá todo el progreso sin guardar", 19f,
                                Palette.TextDim, TextAlignmentOptions.Center);
            Place(hint.rectTransform, ACenter, ACenter, new Vector2(0f, -28f), new Vector2(620f, 26f));

            var no = MakeSmallButton(box, "Btn_No", "QUEDARME", new Vector2(-150f, -105f), new Vector2(260f, 58f), false);
            var yes = MakeSmallButton(box, "Btn_Si", "SALIR", new Vector2(150f, -105f), new Vector2(260f, 58f), true);

            UnityEventTools.AddPersistentListener(no.onClick, controller.CancelarSalida);
            UnityEventTools.AddPersistentListener(yes.onClick, controller.ConfirmarSalida);

            SetNavigationHorizontal(no, yes);
            SetPrivate(panel, "firstSelected", no);
            return panel;
        }

        static MenuButton MakeSmallButton(Transform parent, string name, string label,
                                          Vector2 pos, Vector2 size, bool highlighted)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Place(rt, ACenter, ACenter, pos, size);

            var hit = go.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var button = go.AddComponent<MenuButton>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            var fill = Img("Relleno", rt, Sp("ui_fill"), CFillIdle, Image.Type.Sliced);
            Stretch(fill.rectTransform);

            var frame = Img("Marco", rt, Sp("ui_frame"),
                            highlighted ? Palette.Gold.WithAlpha(0.9f) : CFrameIdle, Image.Type.Sliced);
            Stretch(frame.rectTransform);

            var maskRt = Rect("MascaraDeBrillo", rt);
            Stretch(maskRt);
            maskRt.gameObject.AddComponent<RectMask2D>();

            var shine = Img("Brillo", maskRt, Sp("ui_gradient_h"), Palette.CyanBright.WithAlpha(0f));
            Place(shine.rectTransform, ACenter, ACenter, Vector2.zero, new Vector2(140f, size.y));

            var text = Text("Etiqueta", rt, label, 21f, Palette.Armor, TextAlignmentOptions.Center, MatBody);
            Stretch(text.rectTransform, 12, 0, 12, 0);
            text.characterSpacing = 4f;

            button.fill = fill;
            button.frame = frame;
            button.shine = shine;
            button.label = text;
            button.fillIdle = CFillIdle;
            button.frameIdle = highlighted ? Palette.Gold.WithAlpha(0.9f) : CFrameIdle;
            button.labelIdle = highlighted ? Palette.GoldLight : Palette.Armor;
            button.accentWidthIdle = 0f;
            button.accentWidthActive = 0f;
            button.labelSpacingIdle = 4f;
            button.labelSpacingActive = 7f;
            button.ApplyFocusColors(highlighted);
            return button;
        }

        static void SetNavigationHorizontal(Selectable left, Selectable right)
        {
            left.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = right, selectOnLeft = right };
            right.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = left, selectOnRight = left };
        }

        static void LinkVertical(Selectable[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = items[(i - 1 + items.Length) % items.Length];
                nav.selectOnDown = items[(i + 1) % items.Length];
                items[i].navigation = nav;
            }
        }

        // ==================================================== transicion

        static ScreenTransition BuildTransition(RectTransform parent)
        {
            var root = Rect("05_Transicion", parent);
            Stretch(root);
            var group = Group(root.gameObject);
            group.blocksRaycasts = false;

            var flash = Img("Destello", root, Sp("ui_solid"), Palette.CyanBright.WithAlpha(0f));
            Stretch(flash.rectTransform);

            var black = Img("Negro", root, Sp("ui_solid"), new Color(0.02f, 0.01f, 0.04f, 1f));
            Stretch(black.rectTransform);

            var transition = root.gameObject.AddComponent<ScreenTransition>();
            SetPrivate(transition, "group", group);
            SetPrivate(transition, "fadeImage", black);
            SetPrivate(transition, "flashImage", flash);
            return transition;
        }

        // ====================================================== cableado

        static void WireController(MainMenuController controller,
                                   MenuButton play, MenuButton options, MenuButton credits, MenuButton quit,
                                   UIPanel optionsPanel, UIPanel creditsPanel, UIPanel quitPanel,
                                   ScreenTransition transition, CanvasGroup menuGroup,
                                   CanvasGroup background, CanvasGroup character,
                                   CanvasGroup titleGroup, CanvasGroup dividerGroup, CanvasGroup footer,
                                   TMPTextIntro[] titleIntros,
                                   ParticleField embers, ParticleField stars, EnergyLines lines)
        {
            UnityEventTools.AddPersistentListener(play.onClick, controller.Jugar);
            UnityEventTools.AddPersistentListener(options.onClick, controller.AbrirOpciones);
            UnityEventTools.AddPersistentListener(credits.onClick, controller.AbrirCreditos);
            UnityEventTools.AddPersistentListener(quit.onClick, controller.Salir);

            SetPrivate(controller, "optionsPanel", optionsPanel);
            SetPrivate(controller, "creditsPanel", creditsPanel);
            SetPrivate(controller, "quitPanel", quitPanel);
            SetPrivate(controller, "transition", transition);
            SetPrivate(controller, "menuGroup", menuGroup);
            SetPrivate(controller, "playButton", play);
            SetPrivate(controller, "mainButtons", new[] { play, options, credits, quit });
            SetPrivate(controller, "textIntros", titleIntros);
            SetPrivate(controller, "gameSceneName", "SampleScene");

            var steps = new List<MainMenuController.IntroStep>
            {
                new MainMenuController.IntroStep { label = "fondo",     group = background, duration = 1.1f, delay = 0.0f },
                new MainMenuController.IntroStep { label = "titulo",    group = titleGroup, move = (RectTransform)titleGroup.transform, fromOffset = new Vector2(-70f, 0f), delay = 0.15f, duration = 0.8f, backEase = true },
                new MainMenuController.IntroStep { label = "personaje", group = character,  move = FindChildRect(character.transform, "Ekkar"), fromOffset = new Vector2(120f, -30f), delay = 0.45f, duration = 1.0f },
                new MainMenuController.IntroStep { label = "separador", group = dividerGroup, move = (RectTransform)dividerGroup.transform, fromOffset = new Vector2(-40f, 0f), delay = 1.05f, duration = 0.6f },
                new MainMenuController.IntroStep { label = "pie",       group = footer,     delay = 1.9f, duration = 0.8f },
            };

            var buttons = new[] { play, options, credits, quit };
            for (int i = 0; i < buttons.Length; i++)
            {
                var group = Group(buttons[i].gameObject);
                steps.Add(new MainMenuController.IntroStep
                {
                    label = "boton " + buttons[i].name,
                    group = group,
                    move = (RectTransform)buttons[i].transform,
                    fromOffset = new Vector2(-90f, 0f),
                    delay = 1.15f + i * 0.11f,
                    duration = 0.6f,
                    backEase = true
                });
            }

            SetPrivate(controller, "introSteps", steps);

            var afterIntro = new List<MonoBehaviour>();
            if (embers != null) afterIntro.Add(embers);
            if (lines != null) afterIntro.Add(lines);
            SetPrivate(controller, "enableAfterIntro", afterIntro.ToArray());
        }

        static RectTransform FindChildRect(Transform parent, string name)
        {
            var t = parent.Find(name);
            return t != null ? (RectTransform)t : null;
        }

        // ======================================================= utilidad

        static void AddToBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) list.Add(s);

            const string sample = "Assets/Scenes/SampleScene.unity";
            if (File.Exists(sample) && !list.Exists(s => s.path == sample))
                list.Add(new EditorBuildSettingsScene(sample, true));

            EditorBuildSettings.scenes = list.ToArray();
        }

        /// <summary>Asigna un campo [SerializeField] privado desde el editor.</summary>
        public static void SetPrivate(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[Ekkar] Campo '{field}' no encontrado en {target.GetType().Name}");
                return;
            }
            AssignProperty(prop, value);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignProperty(SerializedProperty prop, object value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.ObjectReference: prop.objectReferenceValue = (Object)value; break;
                case SerializedPropertyType.Float: prop.floatValue = System.Convert.ToSingle(value); break;
                case SerializedPropertyType.Integer: prop.intValue = System.Convert.ToInt32(value); break;
                case SerializedPropertyType.Boolean: prop.boolValue = System.Convert.ToBoolean(value); break;
                case SerializedPropertyType.String: prop.stringValue = (string)value; break;
                case SerializedPropertyType.Color: prop.colorValue = (Color)value; break;
                case SerializedPropertyType.Vector2: prop.vector2Value = (Vector2)value; break;
                case SerializedPropertyType.Vector3: prop.vector3Value = (Vector3)value; break;
                case SerializedPropertyType.Enum: prop.enumValueIndex = System.Convert.ToInt32(value); break;
                default:
                    if (prop.isArray) AssignArray(prop, value);
                    else if (value is Object o) prop.objectReferenceValue = o;
                    break;
            }
        }

        static void AssignArray(SerializedProperty prop, object value)
        {
            if (value is System.Collections.IList list)
            {
                prop.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    var element = prop.GetArrayElementAtIndex(i);
                    if (list[i] is MainMenuController.IntroStep step) AssignIntroStep(element, step);
                    else AssignProperty(element, list[i]);
                }
            }
        }

        static void AssignIntroStep(SerializedProperty prop, MainMenuController.IntroStep step)
        {
            prop.FindPropertyRelative("label").stringValue = step.label;
            prop.FindPropertyRelative("group").objectReferenceValue = step.group;
            prop.FindPropertyRelative("move").objectReferenceValue = step.move;
            prop.FindPropertyRelative("fromOffset").vector2Value = step.fromOffset;
            prop.FindPropertyRelative("delay").floatValue = step.delay;
            prop.FindPropertyRelative("duration").floatValue = step.duration;
            prop.FindPropertyRelative("backEase").boolValue = step.backEase;
        }
    }

    /// <summary>Atajo para dejar configurados los colores "en foco" del boton.</summary>
    public static class MenuButtonSetupExtensions
    {
        public static void ApplyFocusColors(this MenuButton button, bool highlighted)
        {
            button.frameActive = highlighted ? Palette.GoldLight : Palette.CyanBright;
            button.accentActive = highlighted ? Palette.GoldLight : Palette.CyanBright;
            button.fillActive = highlighted
                ? new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.20f)
                : new Color(Palette.Cyan.r, Palette.Cyan.g, Palette.Cyan.b, 0.26f);
            button.labelActive = Color.white;
        }
    }
}
