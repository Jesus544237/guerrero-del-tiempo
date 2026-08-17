using Ekkar.Audio;
using Ekkar.FX;
using Ekkar.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Aplica a la escena abierta, sin reconstruirla:
    ///   - apaga el parpadeo azul de pantalla (TimeStutter y LightningFlash),
    ///   - anade la musica del nivel,
    ///   - anade el sistema de muerte y reaparicion a Ekkar,
    ///   - siembra hogueras temporales (puntos de guardado) por el nivel.
    /// </summary>
    public static class LevelSetupFix
    {
        const string UiArt = "Assets/_Game/Art/UI/Generated";

        [MenuItem("Ekkar/Niveles/Aplicar ajustes de nivel", priority = 43)]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();

            int flickerOff = DisableFlicker();
            AddMusic();
            AddRespawn();
            int checkpoints = AddCheckpoints();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Ekkar] Ajustes aplicados: {flickerOff} efectos de parpadeo apagados, " +
                      $"musica anadida, respawn anadido, {checkpoints} puntos de guardado.");
        }

        /// <summary>Apaga los dos destellos a pantalla completa.</summary>
        static int DisableFlicker()
        {
            int n = 0;

            foreach (var st in Object.FindObjectsByType<TimeStutter>(FindObjectsSortMode.None))
            {
                st.enabled = false;
                EditorUtility.SetDirty(st);
                n++;
            }
            foreach (var lf in Object.FindObjectsByType<LightningFlash>(FindObjectsSortMode.None))
            {
                lf.enabled = false;
                EditorUtility.SetDirty(lf);
                n++;
            }

            // y se dejan los velos totalmente transparentes por si acaso
            foreach (var name in new[] { "DestelloTemporal", "Relampago" })
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                Color c = sr.color;
                c.a = 0f;
                sr.color = c;
                sr.enabled = false;
                EditorUtility.SetDirty(sr);
            }

            return n;
        }

        static void AddMusic()
        {
            var host = GameObject.Find("_Sistemas") ?? new GameObject("_Sistemas");

            if (host.GetComponent<AudioSource>() == null) host.AddComponent<AudioSource>();
            if (host.GetComponent<LevelMusic>() == null) host.AddComponent<LevelMusic>();
            EditorUtility.SetDirty(host);
        }

        static void AddRespawn()
        {
            var ekkar = GameObject.Find("Ekkar");
            if (ekkar == null) { Debug.LogWarning("[Ekkar] No encuentro el objeto Ekkar."); return; }
            if (ekkar.GetComponent<PlayerRespawn>() != null) return;

            var rp = ekkar.AddComponent<PlayerRespawn>();
            var so = new SerializedObject(rp);
            so.FindProperty("levelStart").vector2Value = ekkar.transform.position;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ekkar);
        }

        static int AddCheckpoints()
        {
            var old = GameObject.Find("07_Guardado");
            if (old != null) Object.DestroyImmediate(old);

            var nivel = GameObject.Find("Nivel");
            var root = new GameObject("07_Guardado").transform;
            if (nivel != null) root.SetParent(nivel.transform, false);

            var gear = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArt}/ui_gear_a.png");
            var glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArt}/ui_glow.png");

            // repartidos por el nivel; el ultimo, justo antes del castillo
            float[] positions = { 4f, 22f, 40f, 54f, 68f };

            for (int i = 0; i < positions.Length; i++)
            {
                var go = new GameObject($"Hoguera_{i:00}", typeof(BoxCollider2D), typeof(Checkpoint));
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(positions[i], 1.4f, 0f);

                var box = go.GetComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(1.8f, 3.2f);

                var glowGo = new GameObject("Resplandor", typeof(SpriteRenderer));
                glowGo.transform.SetParent(go.transform, false);
                var glowSr = glowGo.GetComponent<SpriteRenderer>();
                glowSr.sprite = glowSprite;
                glowSr.sortingOrder = 20;
                glowGo.transform.localScale = Vector3.one * 2.2f;

                var symGo = new GameObject("Engranaje", typeof(SpriteRenderer));
                symGo.transform.SetParent(go.transform, false);
                var symSr = symGo.GetComponent<SpriteRenderer>();
                symSr.sprite = gear;
                symSr.sortingOrder = 21;
                symGo.transform.localScale = Vector3.one * 1.1f;
                symGo.AddComponent<UISpin>();

                var cp = go.GetComponent<Checkpoint>();
                var so = new SerializedObject(cp);
                so.FindProperty("id").stringValue = $"medieval_{i:00}";
                so.FindProperty("glow").objectReferenceValue = glowSr;
                so.FindProperty("symbol").objectReferenceValue = symSr;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return positions.Length;
        }
    }
}
