using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>
    /// Flotacion suave (dos senoides desfasadas + balanceo opcional). Captura
    /// su posicion base tras <see cref="startDelay"/> segundos para no pelearse
    /// con la animacion de entrada del menu.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIFloat : MonoBehaviour
    {
        [SerializeField] Vector2 amplitude = new Vector2(0f, 7f);
        [SerializeField] Vector2 period = new Vector2(6f, 4.2f);
        [SerializeField] float rotationAmplitude = 0f;
        [SerializeField] float rotationPeriod = 7f;
        [SerializeField] float startDelay = 0f;
        [SerializeField] bool randomizePhase = true;

        RectTransform _rect;
        Vector2 _basePos;
        float _baseRot;
        float _phaseX, _phaseY, _phaseR;
        float _elapsed;
        bool _captured;

        void Awake()
        {
            _rect = (RectTransform)transform;
            if (randomizePhase)
            {
                _phaseX = Random.Range(0f, Mathf.PI * 2f);
                _phaseY = Random.Range(0f, Mathf.PI * 2f);
                _phaseR = Random.Range(0f, Mathf.PI * 2f);
            }
        }

        void OnEnable()
        {
            _captured = false;
            _elapsed = 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (!_captured)
            {
                _elapsed += dt;
                if (_elapsed < startDelay) return;
                _basePos = _rect.anchoredPosition;
                _baseRot = _rect.localEulerAngles.z;
                _captured = true;
            }

            float t = Time.unscaledTime;
            float x = period.x > 0.01f ? Mathf.Sin(t * Mathf.PI * 2f / period.x + _phaseX) * amplitude.x : 0f;
            float y = period.y > 0.01f ? Mathf.Sin(t * Mathf.PI * 2f / period.y + _phaseY) * amplitude.y : 0f;
            _rect.anchoredPosition = _basePos + new Vector2(x, y);

            if (Mathf.Abs(rotationAmplitude) > 0.01f && rotationPeriod > 0.01f)
            {
                float r = Mathf.Sin(t * Mathf.PI * 2f / rotationPeriod + _phaseR) * rotationAmplitude;
                _rect.localRotation = Quaternion.Euler(0f, 0f, _baseRot + r);
            }
        }
    }
}
