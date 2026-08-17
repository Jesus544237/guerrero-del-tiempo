using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Utilidades para revisar el menu: fija la vista de juego a 1920x1080 y
    /// guarda una captura en la carpeta Screenshots del proyecto.
    /// </summary>
    public static class EkkarCapture
    {
        public const string ShotPath = "Screenshots/menu.png";

        [MenuItem("Ekkar/Utilidades/Fijar Game View a 1920x1080", false, 60)]
        public static void SetGameView1080p()
        {
            try
            {
                var editorAsm = typeof(Editor).Assembly;
                var sizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes = singleton.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);

                var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)GameViewSizeGroupType.Standalone });
                var groupType = group.GetType();

                int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
                var getSize = groupType.GetMethod("GetGameViewSize");

                int index = -1;
                for (int i = 0; i < total; i++)
                {
                    var size = getSize.Invoke(group, new object[] { i });
                    int w = (int)size.GetType().GetProperty("width").GetValue(size);
                    int h = (int)size.GetType().GetProperty("height").GetValue(size);
                    if (w == 1920 && h == 1080) { index = i; break; }
                }

                if (index < 0)
                {
                    var sizeType = editorAsm.GetType("UnityEditor.GameViewSize");
                    var sizeKind = editorAsm.GetType("UnityEditor.GameViewSizeType");
                    var ctor = sizeType.GetConstructor(new[] { sizeKind, typeof(int), typeof(int), typeof(string) });
                    var newSize = ctor.Invoke(new object[] { System.Enum.ToObject(sizeKind, 1), 1920, 1080, "Ekkar 1080p" });
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                    index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
                }

                var gameViewType = editorAsm.GetType("UnityEditor.GameView");
                var window = EditorWindow.GetWindow(gameViewType, false, null, false);
                gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                            .SetValue(window, index);
                window.Repaint();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Ekkar] No pude fijar el tamano de la Game View: " + e.Message);
            }
        }

        [MenuItem("Ekkar/Utilidades/En Play - Abrir OPCIONES", false, 70)]
        public static void OpenOptions() => InvokeOnController("AbrirOpciones");

        [MenuItem("Ekkar/Utilidades/En Play - Abrir CREDITOS", false, 71)]
        public static void OpenCredits() => InvokeOnController("AbrirCreditos");

        [MenuItem("Ekkar/Utilidades/En Play - Abrir SALIR", false, 72)]
        public static void OpenQuit() => InvokeOnController("Salir");

        [MenuItem("Ekkar/Utilidades/En Play - Cerrar panel", false, 73)]
        public static void ClosePanel() => InvokeOnController("CerrarPanelActivo");

        [MenuItem("Ekkar/Utilidades/En Play - Repetir animacion de entrada", false, 74)]
        public static void ReplayIntro() => InvokeOnController("ReproducirIntro");

        // ---- atajos para probar el nivel sin tener que jugarlo ------------

        [MenuItem("Ekkar/Utilidades/En Play - Detener el tiempo", false, 80)]
        public static void StopTime()
        {
            if (!EnMarcha()) return;
            Ekkar.Core.TimeControl.Detener(6f);
        }

        [MenuItem("Ekkar/Utilidades/En Play - Abrir o cerrar la PAUSA", false, 81)]
        public static void TogglePause()
        {
            if (!EnMarcha()) return;
            var pausa = Object.FindAnyObjectByType<Ekkar.UI.PauseMenu>();
            if (pausa == null) { Debug.LogWarning("[Ekkar] No hay menu de pausa en la escena."); return; }
            pausa.Alterna();
        }

        [MenuItem("Ekkar/Utilidades/En Play - Teletransportar Ekkar al jefe", false, 82)]
        public static void WarpToBoss()
        {
            if (!EnMarcha()) return;
            var ekkar = Object.FindAnyObjectByType<Ekkar.Gameplay.EkkarController>();
            if (ekkar == null) { Debug.LogWarning("[Ekkar] No hay Ekkar en esta escena."); return; }

            var jefe = Object.FindAnyObjectByType<Ekkar.Gameplay.BossEncounter>();
            ekkar.transform.position = jefe != null
                ? jefe.transform.position + new Vector3(-7f, 1f, 0f)
                : ekkar.transform.position + new Vector3(30f, 1f, 0f);

            Sana(ekkar.gameObject);
        }

        [MenuItem("Ekkar/Utilidades/En Play - Lanzar la tormenta de rayos", false, 85)]
        public static void Storm()
        {
            if (!EnMarcha()) return;
            var ekkar = Object.FindAnyObjectByType<Ekkar.Gameplay.EkkarController>();
            if (ekkar == null) return;
            // larga a proposito: en el juego dura poco mas de un segundo, pero
            // asi da tiempo a mirarla con calma
            Ekkar.FX.StormBurst.Lanza(ekkar.transform, 3.2f, 1, 12f, 4);
        }

        [MenuItem("Ekkar/Utilidades/En Play - Vaciar la vida del jefe", false, 87)]
        public static void EmptyBoss()
        {
            if (!EnMarcha()) return;
            var jefe = Object.FindAnyObjectByType<Ekkar.Gameplay.BossEncounter>();
            if (jefe == null) { Debug.LogWarning("[Ekkar] No hay jefe en esta escena."); return; }

            var vida = jefe.GetComponent<Ekkar.Gameplay.Damageable>();
            if (vida != null) vida.RecibirGolpe(vida.Vida, jefe.transform.position, true);
        }

        [MenuItem("Ekkar/Utilidades/En Play - Cambiar de pagina en la pausa", false, 86)]
        public static void FlipPause()
        {
            if (!EnMarcha()) return;
            var pausa = Object.FindAnyObjectByType<Ekkar.UI.PauseMenu>();
            if (pausa != null) pausa.PaginaSiguiente();
        }

        [MenuItem("Ekkar/Utilidades/En Play - Reiniciar el nivel", false, 84)]
        public static void RestartLevel()
        {
            if (!EnMarcha()) return;
            var pausa = Object.FindAnyObjectByType<Ekkar.UI.PauseMenu>();
            if (pausa != null) { pausa.Reintentar(); return; }

            Time.timeScale = 1f;
            var escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(escena.name);
        }

        [MenuItem("Ekkar/Utilidades/En Play - Curar a Ekkar", false, 83)]
        public static void HealEkkar()
        {
            if (!EnMarcha()) return;
            var ekkar = Object.FindAnyObjectByType<Ekkar.Gameplay.EkkarController>();
            if (ekkar != null) Sana(ekkar.gameObject);
        }

        static void Sana(GameObject go)
        {
            var combate = go.GetComponent<Ekkar.Gameplay.PlayerCombat>();
            if (combate != null) combate.Revivir();
        }

        static bool EnMarcha()
        {
            if (Application.isPlaying) return true;
            Debug.LogWarning("[Ekkar] Esto solo funciona con el juego en marcha.");
            return false;
        }

        static void InvokeOnController(string method)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Ekkar] Esto solo funciona con el juego en marcha.");
                return;
            }
            var controller = Object.FindFirstObjectByType<Ekkar.UI.MainMenuController>();
            if (controller == null)
            {
                Debug.LogWarning("[Ekkar] No hay MainMenuController en la escena.");
                return;
            }
            controller.SendMessage(method);
        }

        [MenuItem("Ekkar/Utilidades/Capturar Game View", false, 61)]
        public static void Capture()
        {
            Directory.CreateDirectory("Screenshots");
            if (File.Exists(ShotPath)) File.Delete(ShotPath);
            ScreenCapture.CaptureScreenshot(ShotPath, 1);
            Debug.Log("[Ekkar] Captura solicitada: " + ShotPath);
        }
    }
}
