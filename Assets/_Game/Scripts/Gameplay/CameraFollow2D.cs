using UnityEngine;

namespace Ekkar.Gameplay
{
    /// <summary>
    /// Camara de plataformas: sigue al objetivo con retardo, se adelanta hacia
    /// donde mira, y se puede limitar a los bordes del nivel.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] Transform target;

        [Header("Seguimiento")]
        [SerializeField] Vector2 offset = new Vector2(0f, 1.2f);
        [SerializeField] float smoothX = 6f;
        [SerializeField] float smoothY = 3.5f;
        [SerializeField] float lookAhead = 1.8f;
        [SerializeField] float lookAheadSmooth = 3f;

        [Header("Limites del nivel")]
        [SerializeField] bool useBounds = true;
        [SerializeField] Vector2 minBounds = new Vector2(-30f, -2f);
        [SerializeField] Vector2 maxBounds = new Vector2(60f, 12f);

        Camera _cam;
        float _lookOffset;
        float _lastTargetX;

        public void SetTarget(Transform t) => target = t;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (target != null) _lastTargetX = target.position.x;
        }

        void LateUpdate()
        {
            if (target == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // adelanto segun hacia donde se mueve el personaje
            float velocityX = (target.position.x - _lastTargetX) / dt;
            _lastTargetX = target.position.x;
            float desiredLook = Mathf.Clamp(velocityX, -1f, 1f) * lookAhead;
            _lookOffset = Mathf.Lerp(_lookOffset, desiredLook, 1f - Mathf.Exp(-lookAheadSmooth * dt));

            Vector3 goal = new Vector3(
                target.position.x + offset.x + _lookOffset,
                target.position.y + offset.y,
                transform.position.z);

            Vector3 p = transform.position;
            p.x = Mathf.Lerp(p.x, goal.x, 1f - Mathf.Exp(-smoothX * dt));
            p.y = Mathf.Lerp(p.y, goal.y, 1f - Mathf.Exp(-smoothY * dt));

            if (useBounds && _cam.orthographic)
            {
                float halfH = _cam.orthographicSize;
                float halfW = halfH * _cam.aspect;
                p.x = Mathf.Clamp(p.x, minBounds.x + halfW, Mathf.Max(minBounds.x + halfW, maxBounds.x - halfW));
                p.y = Mathf.Clamp(p.y, minBounds.y + halfH, Mathf.Max(minBounds.y + halfH, maxBounds.y - halfH));
            }

            transform.position = p;
        }

        void OnDrawGizmosSelected()
        {
            if (!useBounds) return;
            Gizmos.color = new Color(0.13f, 0.83f, 0.93f, 0.6f);
            Vector3 c = new Vector3((minBounds.x + maxBounds.x) * 0.5f, (minBounds.y + maxBounds.y) * 0.5f, 0f);
            Gizmos.DrawWireCube(c, new Vector3(maxBounds.x - minBounds.x, maxBounds.y - minBounds.y, 0.1f));
        }
    }
}
