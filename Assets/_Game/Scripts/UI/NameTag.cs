using TMPro;
using UnityEngine;

namespace Ekkar.UI
{
    /// <summary>
    /// El nombre del enemigo flotando sobre su cabeza, y si es mini-jefe,
    /// tambien su barra de vida.
    ///
    /// Los enemigos normales no lo llevan puesto todo el rato: se les enciende
    /// un instante al morir y se apaga solo. Asi el jugador se entera de a
    /// quien acaba de matar sin tener la pantalla llena de carteles.
    ///
    /// Se construye a si mismo en cuanto arranca, para no tener que cablear
    /// nada en el prefab.
    /// </summary>
    public class NameTag : MonoBehaviour
    {
        [Header("Que dice")]
        [SerializeField] string nombre = "";
        [SerializeField] TMP_FontAsset fuente;

        [Header("Cuando se ve")]
        [Tooltip("Los mini-jefes lo llevan siempre; los demas solo al caer.")]
        [SerializeField] bool siempreVisible = false;
        [SerializeField] float segundosAlMorir = 2.2f;

        [Header("Aspecto")]
        [SerializeField] float altura = 0.4f;
        [SerializeField] float tamano = 7f;
        [SerializeField] Color color = new Color(0.886f, 0.906f, 0.922f);
        [SerializeField] bool conBarra = false;
        [SerializeField] Color colorBarra = new Color(0.86f, 0.15f, 0.15f);

        Gameplay.Damageable _vida;
        TextMeshPro _texto;
        Transform _barra, _relleno;
        float _apagarEn = -1f;
        float _alto;

        void Awake()
        {
            _vida = GetComponent<Gameplay.Damageable>();

            // el cartel va sobre la cabeza: se mide el sprite para saber donde
            var sr = GetComponentInChildren<SpriteRenderer>();
            _alto = sr != null ? sr.bounds.size.y : 2f;

            Construye();
            Visible(siempreVisible);

            if (_vida != null)
            {
                _vida.Muerto_ += AlMorir;
                _vida.Danado += AlRecibir;
            }
        }

        void OnDestroy()
        {
            if (_vida == null) return;
            _vida.Muerto_ -= AlMorir;
            _vida.Danado -= AlRecibir;
        }

        void Construye()
        {
            var go = new GameObject("Cartel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, _alto + altura, 0f);

            _texto = go.AddComponent<TextMeshPro>();
            _texto.text = string.IsNullOrEmpty(nombre) ? name : nombre;
            _texto.fontSize = tamano;
            _texto.color = color;
            _texto.alignment = TextAlignmentOptions.Center;
            _texto.enableWordWrapping = false;
            if (fuente != null) _texto.font = fuente;

            var rt = _texto.rectTransform;
            rt.sizeDelta = new Vector2(8f, 1.2f);

            // el texto en 3D se dibuja en su propia capa: si no se le pone la
            // misma que a los sprites, se queda detras del decorado
            var mr = _texto.GetComponent<MeshRenderer>();
            var refe = GetComponentInChildren<SpriteRenderer>();
            if (refe != null) mr.sortingLayerID = refe.sortingLayerID;
            mr.sortingOrder = 300;
            _texto.ForceMeshUpdate();

            if (!conBarra) return;

            _barra = NuevaBarra("Barra_Fondo", new Color(0.04f, 0.02f, 0.08f, 0.85f), 118).transform;
            _barra.SetParent(transform, false);
            _barra.localPosition = new Vector3(0f, _alto + altura - 0.22f, 0f);
            _barra.localScale = new Vector3(2.2f, 0.14f, 1f);

            var relleno = NuevaBarra("Barra_Vida", colorBarra, 119);
            _relleno = relleno.transform;
            _relleno.SetParent(_barra, false);
            _relleno.localScale = Vector3.one;
        }

        static GameObject NuevaBarra(string nombre, Color c, int orden)
        {
            var go = new GameObject(nombre, typeof(SpriteRenderer));
            var sr = go.GetComponent<SpriteRenderer>();
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            sr.color = c;
            sr.sortingOrder = orden;
            Ekkar.Core.SpriteMat.Aplica(sr);
            return go;
        }

        void AlRecibir(Gameplay.Damageable d, int queda)
        {
            if (_relleno == null) return;
            float f = d.VidaMaxima > 0 ? queda / (float)d.VidaMaxima : 0f;
            _relleno.localScale = new Vector3(Mathf.Max(0f, f), 1f, 1f);
        }

        void AlMorir(Gameplay.Damageable d)
        {
            if (_relleno != null) _relleno.localScale = new Vector3(0f, 1f, 1f);
            Visible(true);
            _apagarEn = Time.time + segundosAlMorir;
        }

        void Update()
        {
            if (_apagarEn < 0f) return;

            float queda = _apagarEn - Time.time;
            if (queda <= 0f)
            {
                Visible(false);
                _apagarEn = -1f;
                return;
            }

            // se desvanece en el ultimo tercio y sube un poco, como un rotulo
            if (_texto != null)
            {
                var c = _texto.color;
                c.a = Mathf.Clamp01(queda / (segundosAlMorir * 0.4f));
                _texto.color = c;
                _texto.transform.localPosition += Vector3.up * 0.35f * Time.deltaTime;
            }
        }

        /// <summary>
        /// Lo apaga del todo. Lo usa la pelea de jefe: cuando hay barra grande
        /// arriba, el cartel sobre la cabeza es informacion repetida.
        /// </summary>
        public void Oculta()
        {
            siempreVisible = false;
            conBarra = false;
            _apagarEn = -1f;
            Visible(false);
        }

        void Visible(bool on)
        {
            if (_texto != null)
            {
                _texto.gameObject.SetActive(on);
                var c = _texto.color;
                c.a = 1f;
                _texto.color = c;
            }
            if (_barra != null) _barra.gameObject.SetActive(on && conBarra);
        }
    }
}
