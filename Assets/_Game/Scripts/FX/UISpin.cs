using UnityEngine;

namespace Ekkar.FX
{
    /// <summary>Rotacion continua. Se usa en los engranajes del fondo.</summary>
    public class UISpin : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 18f;
        [SerializeField] bool randomizeStartAngle = true;

        void Start()
        {
            if (randomizeStartAngle)
                transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        }

        void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime, Space.Self);
        }
    }
}
