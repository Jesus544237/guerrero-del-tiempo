using System.Collections.Generic;
using System.IO;
using Ekkar.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Crea la escena que abre el juego con la cinematica y la pone la primera
    /// de la lista del build.
    ///
    /// La escena se puede dejar montada aunque todavia no haya video: si no
    /// encuentra ninguno pasa de largo al menu principal. Asi se puede cerrar
    /// esta parte ahora y meter el mp4 cuando este renderizado.
    /// </summary>
    public static class CinematicaBuilder
    {
        const string Ruta = "Assets/_Game/Scenes/00_Intro.unity";
        const string Streaming = "Assets/StreamingAssets";

        [MenuItem("Ekkar/Cinematica/Crear la escena de intro", priority = 2)]
        public static void Crea()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camara = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camara.tag = "MainCamera";
            var cam = camara.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;

            var go = new GameObject("Cinematica");
            go.AddComponent<IntroCinematic>();

            Directory.CreateDirectory(Streaming);
            AssetDatabase.Refresh();

            EditorSceneManager.SaveScene(escena, Ruta);
            PonLaPrimera();

            Debug.Log($"[Ekkar] Escena de intro creada en {Ruta}. " +
                      $"Copia tu video como {Streaming}/intro.mp4 y ya sale al arrancar.");
        }

        /// <summary>La intro va la primera; el resto conserva su orden.</summary>
        static void PonLaPrimera()
        {
            var lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            lista.RemoveAll(s => s.path == Ruta);
            lista.Insert(0, new EditorBuildSettingsScene(Ruta, true));
            EditorBuildSettings.scenes = lista.ToArray();
        }

        [MenuItem("Ekkar/Cinematica/Abrir la carpeta del video", priority = 3)]
        public static void AbreCarpeta()
        {
            Directory.CreateDirectory(Streaming);
            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(Path.GetFullPath(Streaming));
        }
    }
}

