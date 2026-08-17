using System.Collections;
using Ekkar.Core;
using TMPro;
using UnityEngine;

namespace Ekkar.UI
{
    /// <summary>
    /// Anima el texto letra a letra manipulando los vertices de la malla de
    /// TextMeshPro: cada caracter entra desde abajo, escalando y apareciendo,
    /// con un desfase entre letras (el equivalente al SplitText + stagger de
    /// GSAP).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Ekkar/TMP Text Intro")]
    public class TMPTextIntro : MonoBehaviour
    {
        [SerializeField] float startDelay = 0f;
        [SerializeField] float charDelay = 0.045f;
        [SerializeField] float charDuration = 0.55f;
        [SerializeField] float riseDistance = 46f;
        [SerializeField] float startScale = 0.35f;
        [SerializeField] float rotationJitter = 0f;
        [SerializeField] bool playOnEnable = true;

        TMP_Text _text;
        Coroutine _co;
        bool _finished;

        void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            if (playOnEnable) Play();
        }

        void OnDisable()
        {
            _co = null;
        }

        public float TotalDuration
        {
            get
            {
                if (_text == null) _text = GetComponent<TMP_Text>();
                int n = _text != null ? Mathf.Max(1, _text.text.Length) : 1;
                return startDelay + charDelay * n + charDuration;
            }
        }

        public void Play()
        {
            if (!isActiveAndEnabled) return;
            _finished = false;
            Tween.Restart(this, ref _co, Routine());
        }

        /// <summary>Salta al estado final (para cuando el jugador se salta la intro).</summary>
        public void Complete()
        {
            if (_finished) return;
            Tween.Stop(this, ref _co);
            RestoreOriginal();
            _finished = true;
        }

        IEnumerator Routine()
        {
            _text.ForceMeshUpdate();
            TMP_TextInfo info = _text.textInfo;
            if (info == null || info.characterCount == 0) { _finished = true; yield break; }

            TMP_MeshInfo[] cached = info.CopyMeshInfoVertexData();
            int charCount = info.characterCount;

            var seeds = new float[charCount];
            for (int i = 0; i < charCount; i++) seeds[i] = Random.Range(-1f, 1f);

            // arranca con todo invisible
            ApplyFrame(info, cached, seeds, -1f);

            float elapsed = 0f;
            float total = startDelay + charDelay * charCount + charDuration;

            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyFrame(info, cached, seeds, elapsed);
                yield return null;
            }

            RestoreOriginal();
            _finished = true;
            _co = null;
        }

        void ApplyFrame(TMP_TextInfo info, TMP_MeshInfo[] cached, float[] seeds, float elapsed)
        {
            int charCount = info.characterCount;

            for (int i = 0; i < charCount; i++)
            {
                TMP_CharacterInfo ci = info.characterInfo[i];
                if (!ci.isVisible) continue;

                int matIndex = ci.materialReferenceIndex;
                int vertIndex = ci.vertexIndex;

                Vector3[] src = cached[matIndex].vertices;
                Vector3[] dst = info.meshInfo[matIndex].vertices;
                Color32[] dstColors = info.meshInfo[matIndex].colors32;
                Color32[] srcColors = cached[matIndex].colors32;

                float t = Mathf.Clamp01((elapsed - startDelay - i * charDelay) / Mathf.Max(0.0001f, charDuration));
                float move = Ease.OutBack(t);
                float alpha = Ease.OutQuad(Mathf.Clamp01(t * 1.6f));

                Vector3 center = (src[vertIndex] + src[vertIndex + 2]) * 0.5f;
                float scale = Mathf.LerpUnclamped(startScale, 1f, move);
                Vector3 offset = new Vector3(0f, Mathf.LerpUnclamped(-riseDistance, 0f, move), 0f);

                Quaternion rot = Quaternion.identity;
                if (Mathf.Abs(rotationJitter) > 0.01f)
                    rot = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(seeds[i] * rotationJitter, 0f, move));

                for (int k = 0; k < 4; k++)
                {
                    Vector3 local = src[vertIndex + k] - center;
                    dst[vertIndex + k] = center + rot * (local * scale) + offset;

                    Color32 c = srcColors[vertIndex + k];
                    c.a = (byte)Mathf.RoundToInt(c.a * alpha);
                    dstColors[vertIndex + k] = c;
                }
            }

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        void RestoreOriginal()
        {
            if (_text == null) return;
            _text.ForceMeshUpdate();
        }
    }
}
