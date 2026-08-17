using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Compila el juego a distrib/, que es la estructura que pide la evidencia
    /// GA6-220501127-AA1-EV01: una carpeta por plataforma objetivo.
    ///
    /// Deja fuera SampleScene, que es la escena vacia que crea Unity al nacer el
    /// proyecto y que no pinta nada en el juego.
    /// </summary>
    public static class BuildGame
    {
        const string CarpetaWin = "distrib/windows-x64";
        const string Ejecutable = "GuerreroDelTiempo.exe";

        static string RaizProyecto => Directory.GetParent(Application.dataPath).FullName;

        /// <summary>Las escenas activas, en orden, sin la de ejemplo.</summary>
        static string[] Escenas()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .Where(p => !p.Contains("SampleScene"))
                .ToArray();
        }

        [MenuItem("Ekkar/Compilar/Windows 64 bits", priority = 200)]
        public static void Windows()
        {
            var escenas = Escenas();
            if (escenas.Length == 0)
            {
                Debug.LogError("[Build] No hay escenas activas en Build Settings.");
                return;
            }

            var destino = Path.Combine(RaizProyecto, CarpetaWin);
            if (Directory.Exists(destino)) Directory.Delete(destino, true);
            Directory.CreateDirectory(destino);

            Debug.Log($"[Build] {escenas.Length} escenas. Primera: {escenas[0]}");
            Debug.Log($"[Build] Destino: {destino}");

            var opciones = new BuildPlayerOptions
            {
                scenes = escenas,
                locationPathName = Path.Combine(destino, Ejecutable),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            var reporte = BuildPipeline.BuildPlayer(opciones);
            Informe(reporte, destino);
        }

        static void Informe(BuildReport reporte, string destino)
        {
            var r = reporte.summary;
            if (r.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Build] FALLO: {r.result}. Errores: {r.totalErrors}");
                return;
            }

            long total = 0;
            var grandes = new System.Collections.Generic.List<(string, long)>();
            foreach (var f in Directory.GetFiles(destino, "*", SearchOption.AllDirectories))
            {
                var len = new FileInfo(f).Length;
                total += len;
                if (len > 90L * 1024 * 1024)
                    grandes.Add((f.Replace(destino, "").TrimStart('\\', '/'), len));
            }

            Debug.Log($"[Build] OK en {r.totalTime.TotalMinutes:F1} min. " +
                      $"Tamano: {total / 1024f / 1024f:F0} MB en {Directory.GetFiles(destino, "*", SearchOption.AllDirectories).Length} archivos.");

            // GitHub rechaza archivos sueltos de mas de 100 MB si no van por LFS
            if (grandes.Count == 0)
            {
                Debug.Log("[Build] Ningun archivo pasa de 90 MB: la carpeta cabe en GitHub sin LFS.");
            }
            else
            {
                foreach (var (nombre, len) in grandes)
                    Debug.LogWarning($"[Build] Archivo grande: {nombre} = {len / 1024f / 1024f:F0} MB");
                Debug.LogWarning("[Build] Esos archivos necesitan Git LFS o publicar la build como Release.");
            }
        }

        /// <summary>Quita SampleScene de Build Settings, que no pinta nada ahi.</summary>
        [MenuItem("Ekkar/Compilar/Quitar SampleScene de Build Settings", priority = 210)]
        public static void LimpiarEscenas()
        {
            var antes = EditorBuildSettings.scenes.Length;
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Where(s => !s.path.Contains("SampleScene"))
                .ToArray();
            Debug.Log($"[Build] Escenas en Build Settings: {antes} -> {EditorBuildSettings.scenes.Length}");
            AssetDatabase.SaveAssets();
        }
    }
}
