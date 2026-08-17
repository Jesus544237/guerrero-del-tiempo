using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.FX
{
    /// <summary>
    /// Desplaza las coordenadas UV de un RawImage. Se usa para las lineas de
    /// barrido tipo CRT que recorren la pantalla muy despacio.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class ScrollingTexture : MonoBehaviour
    {
        [SerializeField] Vector2 speed = new Vector2(0f, -0.03f);
        [SerializeField] Vector2 tiling = new Vector2(1f, 1f);
        [SerializeField] bool autoTileToScreen = true;
        [SerializeField] float pixelsPerTile = 4f;

        RawImage _image;
        Vector2 _offset;

        void Awake()
        {
            _image = GetComponent<RawImage>();
        }

        void Start()
        {
            if (autoTileToScreen)
            {
                var rect = (RectTransform)transform;
                Vector2 size = rect.rect.size;
                if (size.x > 1f && size.y > 1f && pixelsPerTile > 0.01f)
                    tiling = new Vector2(size.x / pixelsPerTile, size.y / pixelsPerTile);
            }
            Apply();
        }

        void Update()
        {
            _offset += speed * Time.unscaledDeltaTime;
            _offset.x %= 1f;
            _offset.y %= 1f;
            Apply();
        }

        void Apply()
        {
            if (_image == null) return;
            _image.uvRect = new Rect(_offset.x, _offset.y, tiling.x, tiling.y);
        }
    }
}
