using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ekkar.Core;
using Ekkar.FX;
using Ekkar.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Construye la escena del nivel medieval "El Sitio Eterno".
    ///
    /// Composicion: el cielo cubre todo el encuadre, la ciudad lejana acompana
    /// el trayecto como una franja baja, y en el tramo final se disuelve para
    /// dejar el castillo ardiendo contra el cielo. El suelo va justo bajo los
    /// pies de Ekkar. Encima caen pavesas y ceniza, y del castillo y la ciudad
    /// sube humo hacia el cielo.
    /// </summary>
    public static class MedievalSceneBuilder
    {
        const string ArtFolder = "Assets/_Game/Art/Backgrounds/Medieval";
        const string PropsFolder = ArtFolder + "/Props";
        const string UiArt = "Assets/_Game/Art/UI/Generated";
        const string ScenePath = "Assets/_Game/Scenes/01_EdadMedia_SitioEterno.unity";
        const string ControllerPath = "Assets/_Game/Animation/Ekkar/Ekkar.controller";

        const int PPU = 100;
        const float ScreenHeight = 10.8f;
        const float CameraY = 2.6f;
        const float LevelLength = 140f;

        // A partir de aqui la ciudad se disuelve y aparece el castillo
        const float CastleSwitchX = 56f;
        const float CastleBlend = 16f;

        // Alturas de cada capa, medidas para que su base caiga donde toca
        const float SkyY = 2.3f;
        const float CityY = 2.84f;
        const float CastleY = 2.60f;

        // Cuanto acompana cada capa a la camara (0 = fija en el mundo)
        const float SkyFollow = 0.95f;
        const float CityFollow = 0.62f;
        const float CastleFollow = 0.50f;

        [System.Serializable] class PropInfo { public string file; public int width; public int height; }
        [System.Serializable] class LayerInfo { public string key; public int width, height, contentTop, contentBottom; }
        [System.Serializable]
        class Manifest
        {
            public float artScale;
            public int groundTopPixel, groundHeight, groundSolidBottom;
            public LayerInfo[] layers;
            public PropInfo[] props;
        }

        [MenuItem("Ekkar/Niveles/Construir Edad Media", priority = 40)]
        public static void Build()
        {
            var manifest = LoadManifest();
            if (manifest == null) return;

            ConfigureImporters();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = BuildCamera();
            var fondo = new GameObject("Fondo").transform;
            var nivel = new GameObject("Nivel").transform;

            BuildSky(fondo);
            var ciudad = BuildCity(fondo);
            var castillo = BuildCastle(fondo);
            BuildGround(manifest, nivel);
            BuildProps(manifest, nivel);

            var ekkar = BuildEkkar();
            camera.GetComponent<CameraFollow2D>().SetTarget(ekkar.transform);

            BuildOverlays(camera.transform, fondo);
            BuildSectionSwap(camera.transform, ciudad, castillo);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ScenePath)));
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            Debug.Log($"[Ekkar] Escena medieval construida en {ScenePath}");
        }

        static Manifest LoadManifest()
        {
            string p = Path.GetFullPath(ArtFolder + "/medieval_manifest.json");
            if (!File.Exists(p))
            {
                Debug.LogError("[Ekkar] Falta medieval_manifest.json. Ejecuta antes:\n" +
                               "    python Tools/prepare_medieval_layers.py");
                return null;
            }
            return JsonUtility.FromJson<Manifest>(File.ReadAllText(p));
        }

        static void ConfigureImporters()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                bool isProp = path.Contains("/Props/");
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.spritePixelsPerUnit = PPU;
                ti.maxTextureSize = 4096;
                ti.textureCompression = isProp
                    ? TextureImporterCompression.CompressedHQ
                    : TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
        }

        static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";

            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ScreenHeight * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            // El borde superior del cielo es #19284C, no el negro de la paleta.
            // Igualando el color de fondo, la franja que queda por encima del
            // sprite deja de verse como una banda oscura.
            cam.backgroundColor = Palette.FromHex("19284C");
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            go.transform.position = new Vector3(0f, CameraY, -10f);
            go.AddComponent<UniversalAdditionalCameraData>();

            float half = ScreenHeight * 0.5f;
            var follow = go.AddComponent<CameraFollow2D>();
            var so = new SerializedObject(follow);
            so.FindProperty("minBounds").vector2Value = new Vector2(0f, CameraY - half);
            so.FindProperty("maxBounds").vector2Value = new Vector2(LevelLength, CameraY + half);
            so.ApplyModifiedPropertiesWithoutUndo();
            return cam;
        }

        /// <summary>Crea una fila de mosaicos y le pone su ParallaxLayer.</summary>
        static Transform TileRow(string name, Transform parent, Sprite sprite, int order,
                                 float follow, float y, float firstX, int tiles)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = new Vector3(0f, y, 0f);

            float tileW = sprite.rect.width / PPU;
            for (int i = 0; i < tiles; i++)
            {
                var go = new GameObject($"{name}_{i}", typeof(SpriteRenderer));
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(firstX + i * tileW, 0f, 0f);

                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = order;
            }

            var par = root.gameObject.AddComponent<ParallaxLayer>();
            var so = new SerializedObject(par);
            so.FindProperty("factorX").floatValue = follow;
            so.FindProperty("factorY").floatValue = follow;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static void BuildSky(Transform parent)
        {
            var sprite = Load($"{ArtFolder}/bg_cielo.png");
            if (sprite == null) { Debug.LogWarning("[Ekkar] Falta bg_cielo.png"); return; }
            float w = sprite.rect.width / PPU;
            TileRow("01_Cielo", parent, sprite, -100, SkyFollow, SkyY, -w, 3);
        }

        /// <summary>Ciudad lejana: franja baja que acompana casi todo el nivel.</summary>
        static GameObject BuildCity(Transform parent)
        {
            var sprite = Load($"{ArtFolder}/bg_horizonte.png");
            if (sprite == null) { Debug.LogWarning("[Ekkar] Falta bg_horizonte.png"); return null; }

            float w = sprite.rect.width / PPU;
            int tiles = Mathf.CeilToInt((LevelLength * (1f - CityFollow) + 30f) / w);
            var root = TileRow("02_Ciudad", parent, sprite, -90, CityFollow, CityY, -w * 1.5f, tiles);

            var smoke = Load($"{UiArt}/ui_glow.png");
            for (int i = 0; i < 4; i++)
                AddSmoke(root, smoke, new Vector3(-6f + i * 7.5f, -1.6f, 0f),
                         width: 2.2f, height: 5.5f, alpha: 0.22f, order: -89, scale: 0.45f);

            return root.gameObject;
        }

        /// <summary>Castillo ardiendo: solo en el tramo final del nivel.</summary>
        static GameObject BuildCastle(Transform parent)
        {
            var sprite = Load($"{ArtFolder}/bg_castillo.png");
            if (sprite == null) { Debug.LogWarning("[Ekkar] Falta bg_castillo.png"); return null; }

            // Se centra en pantalla cuando la camara llega al final del nivel
            float origin = (LevelLength - 14f) * (1f - CastleFollow);
            var root = TileRow("03_Castillo", parent, sprite, -80, CastleFollow, CastleY, origin, 1);

            var smoke = Load($"{UiArt}/ui_glow.png");
            AddSmoke(root, smoke, new Vector3(origin + 0.8f, 1.4f, 0f), 2.6f, 7.5f, 0.34f, -79, 0.7f);
            AddSmoke(root, smoke, new Vector3(origin + 7.6f, 2.2f, 0f), 2.0f, 6.5f, 0.30f, -79, 0.6f);
            AddSmoke(root, smoke, new Vector3(origin - 5.4f, 0.6f, 0f), 2.4f, 6.0f, 0.26f, -79, 0.55f);

            return root.gameObject;
        }

        static void AddSmoke(Transform parent, Sprite puff, Vector3 localPos,
                             float width, float height, float alpha, int order, float scale)
        {
            var go = new GameObject("Humo");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var fx = go.AddComponent<RisingSmoke>();
            var so = new SerializedObject(fx);
            so.FindProperty("puffSprite").objectReferenceValue = puff;
            so.FindProperty("spawnWidth").floatValue = width;
            so.FindProperty("riseHeight").floatValue = height;
            so.FindProperty("maxAlpha").floatValue = alpha;
            so.FindProperty("sortingOrder").intValue = order;
            so.FindProperty("startScale").vector2Value = new Vector2(scale * 0.7f, scale * 1.4f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildGround(Manifest m, Transform parent)
        {
            var sprite = Load($"{ArtFolder}/bg_suelo.png");
            if (sprite == null) { Debug.LogWarning("[Ekkar] Falta bg_suelo.png"); return; }

            float tileW = sprite.rect.width / PPU;
            float tileH = sprite.rect.height / PPU;

            // la linea pisable, medida desde arriba, se lleva a y = 0
            float walkFromBottom = (m.groundHeight - m.groundTopPixel) / (float)PPU;
            float offsetY = -(walkFromBottom - tileH * 0.5f);

            var root = new GameObject("04_Suelo").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(0f, offsetY, 0f);

            int tiles = Mathf.CeilToInt((LevelLength + tileW * 2f) / tileW);
            for (int i = 0; i < tiles; i++)
            {
                var go = new GameObject($"Suelo_{i}", typeof(SpriteRenderer));
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3((i - 1) * tileW, 0f, 0f);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = -10;
            }

            var col = new GameObject("Colision_Suelo");
            col.transform.SetParent(parent, false);
            col.transform.position = new Vector3(LevelLength * 0.5f, -2f, 0f);
            col.AddComponent<BoxCollider2D>().size = new Vector2(LevelLength + 40f, 4f);

            // Los muros se meten hacia dentro: la camara solo puede centrarse
            // entre [medioAncho, LevelLength - medioAncho], asi que si el
            // jugador pudiera pasar de ahi la camara se quedaria atras y el
            // personaje se saldria del encuadre.
            float halfW = ScreenHeight * 0.5f * (16f / 9f);
            float safe = 0.6f;
            float wallL = safe;
            float wallR = LevelLength - safe;
            _ = halfW;

            foreach (var (x, name) in new[] { (wallL - 0.5f, "Muro_Izq"), (wallR + 0.5f, "Muro_Der") })
            {
                var wall = new GameObject(name);
                wall.transform.SetParent(parent, false);
                wall.transform.position = new Vector3(x, 4f, 0f);
                wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 14f);
            }
        }

        static void BuildProps(Manifest m, Transform parent)
        {
            if (m.props == null || m.props.Length == 0) return;

            var sprites = m.props.Select(p => Load($"{PropsFolder}/{p.file}")).Where(s => s != null).ToList();
            if (sprites.Count == 0) return;

            var back = new GameObject("05_Props_Fondo").transform;
            back.SetParent(parent, false);
            var front = new GameObject("06_Props_Frente").transform;
            front.SetParent(parent, false);

            var rng = new System.Random(20260810);
            float x = 5f;
            int i = 0;
            while (x < LevelLength - 4f)
            {
                bool isFront = rng.NextDouble() < 0.25;
                var sprite = sprites[rng.Next(sprites.Count)];
                float scale = isFront ? 1.25f : (float)(0.8 + rng.NextDouble() * 0.2);
                float h = sprite.rect.height / PPU * scale;

                var go = new GameObject($"Prop_{i:00}", typeof(SpriteRenderer));
                go.transform.SetParent(isFront ? front : back, false);
                go.transform.position = new Vector3(x, h * 0.5f - 0.35f, 0f);
                go.transform.localScale = Vector3.one * scale;

                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = isFront ? 60 : -5;
                sr.color = isFront ? new Color(0.28f, 0.28f, 0.42f) : new Color(0.72f, 0.74f, 0.9f);
                if (rng.NextDouble() < 0.4) sr.flipX = true;

                x += (float)((isFront ? 10.0 : 6.0) + rng.NextDouble() * 6.0);
                i++;
            }

            foreach (var (root, factor) in new[] { (back, 0.05f), (front, -0.12f) })
            {
                var par = root.gameObject.AddComponent<ParallaxLayer>();
                var so = new SerializedObject(par);
                so.FindProperty("factorX").floatValue = factor;
                so.FindProperty("factorY").floatValue = 0f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static GameObject BuildEkkar()
        {
            var root = new GameObject("Ekkar");
            root.transform.position = new Vector3(6f, 0.2f, 0f);

            var body = new GameObject("Sprite", typeof(SpriteRenderer), typeof(Animator));
            body.transform.SetParent(root.transform, false);
            var sr = body.GetComponent<SpriteRenderer>();
            sr.sortingOrder = 0;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null) body.GetComponent<Animator>().runtimeAnimatorController = controller;
            else Debug.LogWarning("[Ekkar] No encuentro Ekkar.controller; importa primero las animaciones.");

            var idle = AssetDatabase
                .LoadAllAssetsAtPath("Assets/_Game/Art/Characters/Ekkar/Anim/ekkar_idle.png")
                .OfType<Sprite>().OrderBy(s => s.name, System.StringComparer.Ordinal).ToArray();
            if (idle.Length > 0) sr.sprite = idle[0];

            root.AddComponent<Rigidbody2D>().freezeRotation = true;

            var col = root.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(0.75f, 2.2f);
            col.offset = new Vector2(0f, 1.1f);
            col.direction = CapsuleDirection2D.Vertical;

            var check = new GameObject("SueloCheck").transform;
            check.SetParent(root.transform, false);
            check.localPosition = new Vector3(0f, 0.05f, 0f);

            var ctrl = root.AddComponent<EkkarController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("groundCheck").objectReferenceValue = check;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static void BuildOverlays(Transform camera, Transform fondo)
        {
            var solid = Load($"{UiArt}/ui_solid.png");
            var vignette = Load($"{UiArt}/ui_vignette.png");
            var dot = Load($"{UiArt}/ui_dot.png");

            SpriteRenderer FullScreen(string name, Sprite sprite, Color color, int order)
            {
                var go = new GameObject(name, typeof(SpriteRenderer));
                go.transform.SetParent(camera, false);
                go.transform.localPosition = new Vector3(0f, 0f, 1f);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = color;
                sr.sortingOrder = order;
                if (sprite != null)
                {
                    sr.drawMode = SpriteDrawMode.Sliced;
                    sr.size = new Vector2(ScreenHeight * 16f / 9f + 0.6f, ScreenHeight + 0.6f);
                }
                return sr;
            }

            FullScreen("Vineta", vignette, new Color(1f, 1f, 1f, 0.8f), 190);
            var lightning = FullScreen("Relampago", solid, new Color(0.65f, 0.8f, 1f, 0f), 195);
            var stutterFlash = FullScreen("DestelloTemporal", solid, new Color(0.13f, 0.83f, 0.93f, 0f), 200);

            // pavesas y ceniza cayendo por todo el recorrido
            var embers = new GameObject("PavesasYCeniza");
            embers.transform.SetParent(camera, false);
            embers.transform.localPosition = new Vector3(0f, 0f, 2f);
            var ef = embers.AddComponent<EmberFall>();
            var eSo = new SerializedObject(ef);
            eSo.FindProperty("moteSprite").objectReferenceValue = dot;
            eSo.ApplyModifiedPropertiesWithoutUndo();

            var ash = new GameObject("CenizaSuspendida");
            ash.transform.SetParent(camera, false);
            ash.transform.localPosition = new Vector3(0f, 0f, 3f);
            var af = ash.AddComponent<SuspendedAsh>();
            var aSo = new SerializedObject(af);
            aSo.FindProperty("moteSprite").objectReferenceValue = dot;
            aSo.FindProperty("count").intValue = 45;
            aSo.FindProperty("sortingOrder").intValue = 55;
            aSo.ApplyModifiedPropertiesWithoutUndo();

            var lf = camera.gameObject.AddComponent<LightningFlash>();
            var lSo = new SerializedObject(lf);
            lSo.FindProperty("overlay").objectReferenceValue = lightning;
            lSo.ApplyModifiedPropertiesWithoutUndo();

            var systems = new GameObject("_Sistemas");
            var stutter = systems.AddComponent<TimeStutter>();
            var sSo = new SerializedObject(stutter);
            sSo.FindProperty("flashOverlay").objectReferenceValue = stutterFlash;
            var targets = sSo.FindProperty("targets");
            var list = new List<Transform>();
            foreach (Transform child in fondo) list.Add(child);
            targets.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
                targets.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            sSo.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildSectionSwap(Transform camera, GameObject ciudad, GameObject castillo)
        {
            if (ciudad == null || castillo == null) return;

            var go = new GameObject("CambioDeTramo");
            var swap = go.AddComponent<SectionSwap>();
            var so = new SerializedObject(swap);
            so.FindProperty("target").objectReferenceValue = camera;
            so.FindProperty("before").objectReferenceValue = ciudad;
            so.FindProperty("after").objectReferenceValue = castillo;
            so.FindProperty("switchX").floatValue = CastleSwitchX;
            so.FindProperty("blendWidth").floatValue = CastleBlend;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void RegisterInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath))
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
