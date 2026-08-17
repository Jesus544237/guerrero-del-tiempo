using Ekkar.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ekkar.EditorTools
{
    /// <summary>
    /// Cuadra los muros invisibles con lo que la camara puede llegar a
    /// encuadrar.
    ///
    /// La camara se limita al rectangulo minBounds..maxBounds, asi que su
    /// centro nunca sale de [minX + medioAncho, maxX - medioAncho]. Si los
    /// muros dejan al jugador ir mas alla de ese rango, la camara se queda
    /// atras y el personaje se despega hasta salirse del encuadre. Aqui se
    /// recolocan los muros para que el jugador siempre quede dentro, con un
    /// margen de seguridad.
    ///
    /// Es aditivo: solo mueve los dos muros, no toca las capas ni el arte.
    /// </summary>
    public static class LevelBoundsFix
    {
        const float SafeMargin = 2.5f;   // unidades que el jugador nunca pisa cerca del borde

        [MenuItem("Ekkar/Niveles/Ajustar muros al encuadre", priority = 42)]
        public static void Fix()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[Ekkar] No hay MainCamera en la escena."); return; }

            var follow = cam.GetComponent<CameraFollow2D>();
            if (follow == null) { Debug.LogError("[Ekkar] La camara no tiene CameraFollow2D."); return; }

            var so = new SerializedObject(follow);
            Vector2 min = so.FindProperty("minBounds").vector2Value;
            Vector2 max = so.FindProperty("maxBounds").vector2Value;

            float halfH = cam.orthographicSize;
            float halfW = halfH * (16f / 9f);

            // recorrido real del centro de la camara
            float camMinX = min.x + halfW;
            float camMaxX = Mathf.Max(camMinX, max.x - halfW);

            // el jugador debe caber en pantalla en ambos extremos
            float playerMinX = camMinX - halfW + SafeMargin;
            float playerMaxX = camMaxX + halfW - SafeMargin;

            var left = GameObject.Find("Muro_Izq");
            var right = GameObject.Find("Muro_Der");
            if (left == null || right == null)
            {
                Debug.LogError("[Ekkar] No encuentro Muro_Izq / Muro_Der en la escena.");
                return;
            }

            var lp = left.transform.position;
            var rp = right.transform.position;
            float lw = left.GetComponent<BoxCollider2D>() != null ? left.GetComponent<BoxCollider2D>().size.x : 1f;
            float rw = right.GetComponent<BoxCollider2D>() != null ? right.GetComponent<BoxCollider2D>().size.x : 1f;

            left.transform.position = new Vector3(playerMinX - lw * 0.5f, lp.y, lp.z);
            right.transform.position = new Vector3(playerMaxX + rw * 0.5f, rp.y, rp.z);

            Undo.RecordObject(left.transform, "Ajustar muros");
            Undo.RecordObject(right.transform, "Ajustar muros");
            EditorUtility.SetDirty(left);
            EditorUtility.SetDirty(right);
            EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
            EditorSceneManager.SaveScene(cam.gameObject.scene);

            Debug.Log(
                $"[Ekkar] medioAncho={halfW:0.00}  camara recorre x {camMinX:0.0}..{camMaxX:0.0}\n" +
                $"        jugador limitado a x {playerMinX:0.0}..{playerMaxX:0.0}\n" +
                $"        Muro_Izq -> {left.transform.position.x:0.0}   Muro_Der -> {right.transform.position.x:0.0}");
        }
    }
}
