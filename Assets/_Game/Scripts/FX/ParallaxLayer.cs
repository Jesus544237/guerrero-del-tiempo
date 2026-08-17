using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>
    /// Capa de parallax en mundo. Se desplaza una fraccion de lo que se mueve
    /// la camara: 0 = pegada al fondo del universo (no se mueve nunca),
    /// 1 = clavada a la camara. Opcionalmente repite la textura en horizontal
    /// para que la capa sea infinita.
    /// </summary>
    [ExecuteAlways]
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("Profundidad")]
        [Range(0f, 1f)] public float factorX = 0.5f;
        [Range(0f, 1f)] public float factorY = 0.2f;

        [Header("Repeticion infinita")]
        public bool infiniteX = false;
        [Tooltip("Ancho en unidades de mundo de un mosaico. 0 = se calcula del sprite.")]
        public float tileWidth = 0f;

        [Header("Deriva propia (nubes, niebla)")]
        public float driftSpeed = 0f;

        Transform _cam;
        Vector3 _origin;
        float _drift;
        Vector3 _camStart;
        bool _hasCamStart;

        void OnEnable()
        {
            _origin = transform.position;
            _hasCamStart = false;
            AcquireCamera();
            if (tileWidth <= 0.01f) tileWidth = MeasureTileWidth();
        }

        void AcquireCamera()
        {
            var cam = Camera.main;
#if UNITY_EDITOR
            if (cam == null && !Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
                cam = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
            _cam = cam != null ? cam.transform : null;
        }

        float MeasureTileWidth()
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            return sr != null ? sr.bounds.size.x : 0f;
        }

        void LateUpdate()
        {
            // Fuera de juego la capa se queda donde la dejo el constructor: asi
            // lo que se ve en el editor es exactamente el primer fotograma.
            if (!Application.isPlaying) return;

            if (_cam == null) { AcquireCamera(); if (_cam == null) return; }

            if (!_hasCamStart)
            {
                _camStart = _cam.position;
                _hasCamStart = true;
            }

            if (Mathf.Abs(driftSpeed) > 0.0001f)
                _drift += driftSpeed * Time.deltaTime;

            // El desplazamiento es relativo a DONDE ARRANCO la camara. Usar su
            // posicion absoluta metia un salto fijo de camaraY * factorY, que
            // levantaba el cielo y abria un hueco negro sobre el horizonte.
            Vector3 camPos = _cam.position;
            Vector3 delta = camPos - _camStart;

            float x = _origin.x + delta.x * factorX + _drift;
            float y = _origin.y + delta.y * factorY;

            if (infiniteX && tileWidth > 0.01f)
            {
                // reengancha la capa al mosaico mas cercano a la camara
                float relative = camPos.x - x;
                float shift = Mathf.Round(relative / tileWidth) * tileWidth;
                x += shift;
            }

            transform.position = new Vector3(x, y, _origin.z);
        }

        /// <summary>Vuelve a tomar la posicion actual como origen de la capa.</summary>
        public void RebaseOrigin()
        {
            _origin = transform.position;
        }
    }
}
