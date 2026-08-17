using Ekkar.Core;
using UnityEngine;

namespace Ekkar.Gameplay
{
    /// <summary>
    /// Lo que sale de verdad cuando el jefe ataca.
    ///
    /// El arte del Senor del Tiempo trae siete ataques distintos — agujas, eco,
    /// pendulo, grieta, reloj, enjambre y el final — pero todos hacian lo mismo:
    /// una caja de golpe delante. Se veia una animacion espectacular y no salia
    /// nada de ella, asi que la pelea entera era acercarse y pegar.
    ///
    /// Aqui cada animacion tiene su forma de hacer dano. El nombre del clip es
    /// la clave: <see cref="EnemyBrain"/> avisa en el fotograma del impacto con
    /// el ataque que esta reproduciendo, y esto decide que sale.
    /// </summary>
    public class BossAttackPatterns : MonoBehaviour
    {
        static readonly Color Cian = new Color(0.13f, 0.83f, 0.93f);
        static readonly Color Oro = new Color(0.98f, 0.78f, 0.22f);
        static readonly Color Violeta = new Color(0.66f, 0.42f, 0.98f);

        [Header("Fuerza")]
        [SerializeField] int danoProyectil = 1;
        [SerializeField] int danoGrieta = 2;
        [SerializeField] float alturaBoca = 1.6f;

        Transform _ekkar;

        void Start()
        {
            var jugador = FindAnyObjectByType<EkkarController>();
            if (jugador != null) _ekkar = jugador.transform;
        }

        /// <summary>
        /// Devuelve true si este ataque ya ha hecho lo suyo y el cerebro no
        /// tiene que repartir su golpe cuerpo a cuerpo de siempre.
        /// </summary>
        public bool Ejecuta(string ataque, int sentido, Vector2 pecho)
        {
            switch (ataque)
            {
                case "ataque_agujas": Agujas(sentido, pecho); return true;
                case "ataque_eco": Eco(sentido, pecho); return true;
                case "fase2_ataque_grieta": Grietas(); return true;
                case "fase2_ataque_reloj": Reloj(pecho); return true;
                case "fase2_ataque_enjambre_opcion_1":
                case "fase2_ataque_enjambre_opcion_2": Enjambre(sentido, pecho); return true;
                case "ataque_final": Final(sentido, pecho); return true;

                // el pendulo es lo que siempre fue: un mandoble de cerca
                default: return false;
            }
        }

        // ------------------------------------------------------------ patrones

        /// <summary>Tres agujas de reloj en abanico hacia donde este Ekkar.</summary>
        void Agujas(int sentido, Vector2 pecho)
        {
            Vector2 haciaEkkar = Apunta(pecho, sentido);
            for (int i = -1; i <= 1; i++)
            {
                Vector2 dir = Gira(haciaEkkar, i * 13f);
                Projectile.Lanzar(Boca(pecho, sentido), dir, Damageable.Bando.Enemigo,
                                  danoProyectil, 8.5f, 4f, null, Oro);
            }
            Audio.Sfx.Play("espada", 0.7f, 1.4f);
        }

        /// <summary>Una onda lenta y grande que barre el suelo.</summary>
        void Eco(int sentido, Vector2 pecho)
        {
            var origen = new Vector3(pecho.x + sentido * 1.2f, transform.position.y + 0.5f, 0f);
            Projectile.Lanzar(origen, new Vector2(sentido, 0f), Damageable.Bando.Enemigo,
                              danoProyectil, 5.5f, 5f, null, Violeta);
            Audio.Sfx.Play("chrono", 0.6f, 1.1f);
        }

        /// <summary>Ocho agujas en circulo, despacio: se puede cruzar entre ellas.</summary>
        void Reloj(Vector2 pecho)
        {
            const int cuantas = 8;
            for (int i = 0; i < cuantas; i++)
            {
                float ang = i * (360f / cuantas) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Projectile.Lanzar(pecho + dir * 0.8f, dir, Damageable.Bando.Enemigo,
                                  danoProyectil, 5.5f, 3.5f, null, Cian);
            }
            Audio.Sfx.Play("detener", 0.8f, 1.2f);
        }

        /// <summary>Cuatro esquirlas en abanico cerrado.</summary>
        void Enjambre(int sentido, Vector2 pecho)
        {
            Vector2 haciaEkkar = Apunta(pecho, sentido);
            for (int i = 0; i < 4; i++)
            {
                Vector2 dir = Gira(haciaEkkar, Random.Range(-22f, 22f));
                Projectile.Lanzar(Boca(pecho, sentido), dir, Damageable.Bando.Enemigo,
                                  danoProyectil, Random.Range(7f, 10f), 3.5f, null, Violeta);
            }
            Audio.Sfx.Play("espada", 0.75f, 1.6f);
        }

        /// <summary>
        /// Dos grietas en el suelo alrededor de donde este Ekkar. Avisan tres
        /// cuartos de segundo antes de reventar: un jefe que te mata sin darte
        /// tiempo a leerlo no es dificil, es injusto.
        /// </summary>
        void Grietas()
        {
            if (_ekkar == null) return;

            float x = _ekkar.position.x;
            float y = _ekkar.position.y;
            float[] desvios = { 0f, -3.2f };
            for (int i = 0; i < desvios.Length; i++)
                GrietaDelSuelo.Abre(new Vector2(x + desvios[i], y), danoGrieta, 0.75f + i * 0.18f);

            Audio.Sfx.Play("impacto", 0.8f, 0.6f);
        }

        /// <summary>El definitivo: el circulo entero y el abanico a la vez.</summary>
        void Final(int sentido, Vector2 pecho)
        {
            Reloj(pecho);
            Agujas(sentido, pecho);
            Audio.Sfx.Play("chrono", 1f, 0.8f);
        }

        // ------------------------------------------------------------ ayudas

        Vector3 Boca(Vector2 pecho, int sentido)
            => new Vector3(pecho.x + sentido * 1.4f, transform.position.y + alturaBoca, 0f);

        /// <summary>Direccion hacia Ekkar; si no lo encuentra, recto hacia delante.</summary>
        Vector2 Apunta(Vector2 desde, int sentido)
        {
            if (_ekkar == null) return new Vector2(sentido, 0f);
            Vector2 d = ((Vector2)_ekkar.position + Vector2.up * 1f) - desde;
            return d.sqrMagnitude < 0.01f ? new Vector2(sentido, 0f) : d.normalized;
        }

        static Vector2 Gira(Vector2 v, float grados)
        {
            float r = grados * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }

    /// <summary>
    /// Una grieta que se abre en el suelo: primero marca el sitio y luego
    /// revienta hacia arriba. El aviso es la mitad del ataque.
    /// </summary>
    public class GrietaDelSuelo : MonoBehaviour
    {
        static readonly Color Aviso = new Color(0.98f, 0.35f, 0.25f);
        static readonly Color Fuego = new Color(0.75f, 0.45f, 1f);

        SpriteRenderer _marca, _columna;
        float _revientaEn, _muereEn;
        int _dano;
        bool _reventada;

        public static GrietaDelSuelo Abre(Vector2 donde, int dano, float aviso)
        {
            var go = new GameObject("GrietaDelSuelo");
            go.transform.position = donde;

            var g = go.AddComponent<GrietaDelSuelo>();
            g._dano = dano;
            g._revientaEn = Time.time + aviso;
            g._muereEn = g._revientaEn + 0.5f;
            g.Construye();
            return g;
        }

        void Construye()
        {
            _marca = Pieza("Marca", Aviso, 46);
            _marca.transform.localScale = new Vector3(1.6f, 0.18f, 1f);

            _columna = Pieza("Columna", Fuego, 47);
            _columna.transform.localScale = Vector3.zero;
            _columna.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        }

        SpriteRenderer Pieza(string nombre, Color c, int orden)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Blanco();
            sr.color = c;
            sr.sortingOrder = orden;
            SpriteMat.Aplica(sr);
            return sr;
        }

        void Update()
        {
            // con el tiempo parado la grieta espera su turno como todo lo demas
            if (TimeControl.Detenido)
            {
                _revientaEn += Time.deltaTime;
                _muereEn += Time.deltaTime;
                return;
            }

            if (!_reventada)
            {
                float queda = _revientaEn - Time.time;
                if (queda > 0f)
                {
                    // parpadea cada vez mas rapido segun se acerca
                    float ritmo = Mathf.Lerp(22f, 6f, Mathf.Clamp01(queda));
                    float a = Mathf.Repeat(Time.time * ritmo, 1f) < 0.55f ? 0.85f : 0.25f;
                    _marca.color = new Color(Aviso.r, Aviso.g, Aviso.b, a);
                    return;
                }

                _reventada = true;
                Damageable.Golpear(transform.position + Vector3.up * 1.2f, 1.5f,
                                   Damageable.Bando.Enemigo, _dano);
                Audio.Sfx.Play("impacto", 0.9f, 0.75f);
            }

            // la columna sube y se apaga
            float t = Mathf.Clamp01(1f - (_muereEn - Time.time) / 0.5f);
            _columna.transform.localScale = new Vector3(1.1f, Mathf.Lerp(0.2f, 3.2f, t), 1f);
            _columna.transform.localPosition = new Vector3(0f, _columna.transform.localScale.y * 0.5f, 0f);
            _columna.color = new Color(Fuego.r, Fuego.g, Fuego.b, 1f - t);
            _marca.color = new Color(Aviso.r, Aviso.g, Aviso.b, (1f - t) * 0.6f);

            if (Time.time >= _muereEn) Destroy(gameObject);
        }

        static Sprite _blanco;

        static Sprite Blanco()
        {
            if (_blanco != null) return _blanco;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _blanco = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _blanco;
        }
    }
}
