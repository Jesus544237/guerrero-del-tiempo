using UnityEngine;
using UnityEngine.UI;

namespace Ekkar.FX
{
    /// <summary>
    /// Reproduce una secuencia de sprites sobre un Image de UI. Se usa para la
    /// animacion "idle" de Ekkar en el menu sin necesidad de Animator.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SpriteSequence : MonoBehaviour
    {
        [SerializeField] Sprite[] frames;
        [SerializeField] float framesPerSecond = 12f;
        [SerializeField] bool loop = true;
        [SerializeField] bool randomStartFrame = false;

        Image _image;
        float _timer;
        int _index;

        public int FrameCount => frames != null ? frames.Length : 0;
        public Sprite CurrentSprite => _image != null ? _image.sprite : null;

        void Awake()
        {
            _image = GetComponent<Image>();
            if (frames != null && frames.Length > 0)
            {
                if (randomStartFrame) _index = Random.Range(0, frames.Length);
                _image.sprite = frames[_index];
            }
        }

        void Update()
        {
            if (frames == null || frames.Length < 2 || framesPerSecond <= 0.01f) return;

            _timer += Time.unscaledDeltaTime;
            float step = 1f / framesPerSecond;
            while (_timer >= step)
            {
                _timer -= step;
                _index++;
                if (_index >= frames.Length)
                {
                    if (!loop) { _index = frames.Length - 1; break; }
                    _index = 0;
                }
                _image.sprite = frames[_index];
            }
        }
    }
}
