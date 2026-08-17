using System.Linq;
using Ekkar.Core;
using Ekkar.Gameplay;
using Ekkar.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Monta la pantalla de resultado (derrota / fragmento / victoria) en cada
    /// nivel, junto con LevelFlow y la meta del final del recorrido.
    ///
    /// Usa la misma tipografia pixel del titulo del menu, pero el degradado
    /// cambia segun la era, de forma que el color ya te dice donde estas.
    /// </summary>
    public static class ResultScreenBuilder
    {
        const string UiArt = "Assets/_Game/Art/UI/Generated";
        const string FontPath = "Assets/_Game/Art/Fonts/Ekkar_Pixel_SDF.asset";
        const string MatTitle = "Assets/_Game/Art/Fonts/M_Texto_Titulo.mat";

        struct LevelInfo
        {
            public string scene, next, title, subtitle;
            public int index;
            public bool boss;
            public Color top, bottom;
            public float goalX;
        }

        static readonly LevelInfo[] Levels =
        {
            new LevelInfo { scene = "01_EdadMedia_SitioEterno", next = "02_EraIndustrial_FundicionDeLasHoras",
                index = 1, goalX = 78f, title = "EL BADAJO ES TUYO",
                subtitle = "Valdheim termina por fin su ultima noche.",
                top = new Color(0.98f, 0.75f, 0.14f), bottom = new Color(0.86f, 0.15f, 0.15f) },

            new LevelInfo { scene = "02_EraIndustrial_FundicionDeLasHoras", next = "03_FuturoDigital_NeonSinManana",
                index = 2, goalX = 78f, title = "EL VOLANTE ES TUYO",
                subtitle = "La fundicion apaga sus hornos para siempre.",
                top = new Color(1f, 0.72f, 0.16f), bottom = new Color(0.82f, 0.25f, 0.08f) },

            new LevelInfo { scene = "03_FuturoDigital_NeonSinManana", next = "04_HoraCero_VacioEntreSegundos",
                index = 3, goalX = 78f, title = "EL CUARZO ES TUYO",
                subtitle = "La ciudad podra guardar el dia siguiente.",
                top = new Color(0.13f, 0.90f, 0.95f), bottom = new Color(0.85f, 0.20f, 0.75f) },

            new LevelInfo { scene = "04_HoraCero_VacioEntreSegundos", next = "",
                index = 4, goalX = 54f, boss = true, title = "EL TIEMPO VUELVE A CORRER",
                subtitle = "El Gran Reloj late otra vez.",
                top = new Color(1f, 0.85f, 0.25f), bottom = new Color(0.13f, 0.90f, 0.95f) },
        };

        [MenuItem("Ekkar/Niveles/Anadir pantallas de resultado", priority = 55)]
        public static void ApplyAll()
        {
            foreach (var info in Levels)
            {
                string path = $"Assets/_Game/Scenes/{info.scene}.unity";
                if (!System.IO.File.Exists(System.IO.Path.GetFullPath(path)))
                {
                    Debug.LogWarning($"[Ekkar] No existe {path}");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Build(info);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Ekkar] Pantalla de resultado anadida a {info.scene}");
            }
            AssetDatabase.SaveAssets();
        }

        static void Build(LevelInfo info)
        {
            EnsureEventSystem();

            var old = GameObject.Find("Canvas_Resultado");
            if (old != null) Object.DestroyImmediate(old);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var titleMat = AssetDatabase.LoadAssetAtPath<Material>(MatTitle);
            var solid = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArt}/ui_solid.png");
            var glow = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArt}/ui_glow.png");
            var frame = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArt}/ui_frame.png");
            var fill = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArt}/ui_fill.png");

            // ---- canvas
            var canvasGo = new GameObject("Canvas_Resultado",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)canvasGo.transform;

            Image FullScreen(string name, Sprite sprite, Color color)
            {
                var go = new GameObject(name, typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(root, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.sprite = sprite;
                img.color = color;
                return img;
            }

            var scrim = FullScreen("Velo", solid, new Color(0.04f, 0.02f, 0.08f, 0f));

            // rayos de victoria
            var raysGo = new GameObject("Rayos", typeof(Image));
            var raysRt = (RectTransform)raysGo.transform;
            raysRt.SetParent(root, false);
            raysRt.anchorMin = raysRt.anchorMax = new Vector2(0.5f, 0.5f);
            raysRt.sizeDelta = new Vector2(1700, 1700);
            raysRt.anchoredPosition = new Vector2(0f, 60f);
            var raysImg = raysGo.GetComponent<Image>();
            raysImg.sprite = glow;
            raysImg.color = new Color(info.top.r, info.top.g, info.top.b, 0.18f);
            raysGo.SetActive(false);

            var flash = FullScreen("Destello", solid, new Color(1f, 1f, 1f, 0f));

            // ---- titulo
            TMP_Text Text(string name, string content, float size, Vector2 pos, Vector2 sizeDelta,
                          Color color, Material mat)
            {
                var go = new GameObject(name, typeof(TextMeshProUGUI));
                var rt = (RectTransform)go.transform;
                rt.SetParent(root, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = sizeDelta;

                var t = go.GetComponent<TextMeshProUGUI>();
                t.text = content;
                t.fontSize = size;
                t.alignment = TextAlignmentOptions.Center;
                t.color = color;
                t.enableWordWrapping = true;
                if (font != null) t.font = font;
                if (mat != null) t.fontSharedMaterial = mat;
                return t;
            }

            var title = Text("Titulo", info.title, 86f, new Vector2(0f, 160f),
                             new Vector2(1600f, 320f), Color.white, titleMat);
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(info.top, info.top, info.bottom, info.bottom);
            title.characterSpacing = 6f;

            var intro = title.gameObject.AddComponent<TMPTextIntro>();
            var iso = new SerializedObject(intro);
            iso.FindProperty("charDelay").floatValue = 0.035f;
            iso.FindProperty("riseDistance").floatValue = 60f;
            iso.FindProperty("playOnEnable").boolValue = false;
            iso.ApplyModifiedPropertiesWithoutUndo();

            var subtitle = Text("Subtitulo", info.subtitle, 30f, new Vector2(0f, -20f),
                                new Vector2(1300f, 120f), Palette.TextDim, null);

            // ---- botones
            var rowGo = new GameObject("Botones", typeof(RectTransform));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(root, false);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = new Vector2(0f, -220f);
            row.sizeDelta = new Vector2(1000f, 90f);

            (Button btn, TMP_Text label) MakeButton(string name, float x)
            {
                var go = new GameObject(name, typeof(Image), typeof(Button));
                var rt = (RectTransform)go.transform;
                rt.SetParent(row, false);
                rt.anchoredPosition = new Vector2(x, 0f);
                rt.sizeDelta = new Vector2(400f, 82f);

                var bg = go.GetComponent<Image>();
                bg.sprite = fill;
                bg.type = Image.Type.Sliced;
                bg.color = new Color(0.10f, 0.04f, 0.18f, 0.85f);

                var frameGo = new GameObject("Marco", typeof(Image));
                var frt = (RectTransform)frameGo.transform;
                frt.SetParent(rt, false);
                frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
                frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
                var fimg = frameGo.GetComponent<Image>();
                fimg.sprite = frame;
                fimg.type = Image.Type.Sliced;
                fimg.color = info.top;
                fimg.raycastTarget = false;

                var lbl = new GameObject("Texto", typeof(TextMeshProUGUI));
                var lrt = (RectTransform)lbl.transform;
                lrt.SetParent(rt, false);
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                var lt = lbl.GetComponent<TextMeshProUGUI>();
                lt.text = name;
                lt.fontSize = 30f;
                lt.alignment = TextAlignmentOptions.Center;
                lt.color = Palette.Armor;
                lt.raycastTarget = false;
                if (font != null) lt.font = font;

                var b = go.GetComponent<Button>();
                b.targetGraphic = bg;
                var colors = b.colors;
                colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
                colors.selectedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
                b.colors = colors;

                return (b, lt);
            }

            var (primary, primaryLabel) = MakeButton("Principal", -210f);
            var (secondary, secondaryLabel) = MakeButton("Secundario", 210f);

            // ---- componente
            var rs = canvasGo.AddComponent<ResultScreen>();
            var so = new SerializedObject(rs);
            so.FindProperty("group").objectReferenceValue = canvasGo.GetComponent<CanvasGroup>();
            so.FindProperty("scrim").objectReferenceValue = scrim;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("subtitleText").objectReferenceValue = subtitle;
            so.FindProperty("titleIntro").objectReferenceValue = intro;
            so.FindProperty("buttonRow").objectReferenceValue = row;
            so.FindProperty("primaryButton").objectReferenceValue = primary;
            so.FindProperty("secondaryButton").objectReferenceValue = secondary;
            so.FindProperty("primaryLabel").objectReferenceValue = primaryLabel;
            so.FindProperty("secondaryLabel").objectReferenceValue = secondaryLabel;
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("rays").objectReferenceValue = raysRt;
            so.FindProperty("eraTopColor").colorValue = info.top;
            so.FindProperty("eraBottomColor").colorValue = info.bottom;
            so.FindProperty("levelTitle").stringValue = info.title;
            so.FindProperty("levelSubtitle").stringValue = info.subtitle;
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEventTools.AddPersistentListener(primary.onClick, rs.OnPrimary);
            UnityEventTools.AddPersistentListener(secondary.onClick, rs.OnSecondary);

            // ---- flujo del nivel
            var flowGo = GameObject.Find("_Flujo") ?? new GameObject("_Flujo");
            var flow = flowGo.GetComponent<LevelFlow>() ?? flowGo.AddComponent<LevelFlow>();
            var fso = new SerializedObject(flow);
            fso.FindProperty("levelIndex").intValue = info.index;
            fso.FindProperty("nextSceneName").stringValue = info.next;
            fso.FindProperty("isFinalBossLevel").boolValue = info.boss;
            fso.FindProperty("resultScreen").objectReferenceValue = rs;
            fso.ApplyModifiedPropertiesWithoutUndo();

            // ---- meta al final del recorrido
            var goalOld = GameObject.Find("Meta");
            if (goalOld != null) Object.DestroyImmediate(goalOld);

            var goal = new GameObject("Meta", typeof(BoxCollider2D), typeof(LevelGoal));
            goal.transform.position = new Vector3(info.goalX, 3f, 0f);
            var gbox = goal.GetComponent<BoxCollider2D>();
            gbox.isTrigger = true;
            gbox.size = new Vector2(2.5f, 10f);
            var gso = new SerializedObject(goal.GetComponent<LevelGoal>());
            gso.FindProperty("flow").objectReferenceValue = flow;
            gso.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
