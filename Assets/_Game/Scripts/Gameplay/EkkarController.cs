using Ekkar.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ekkar.Gameplay
{
    /// <summary>
    /// Ekkar entero: moverse, saltar, pegar y usar sus habilidades.
    ///
    /// Controles:
    ///   A/D o flechas  mover        ESPACIO/W  saltar (y salto doble)
    ///   SHIFT          dash         J          atacar     I o W+J  ataque alto
    ///   K              golpe cargado (gasta mana)
    ///   E              detener el tiempo       R          chronobreak
    ///   Q              envainar / desenvainar  ESC o P    pausa
    ///
    /// El salto NO es infinito: solo hay dos, y el contador solo se rellena
    /// tocando suelo de verdad. El suelo se detecta ignorando los disparadores,
    /// que es donde estaba la trampa: los enemigos llevan collider de tipo
    /// trigger, y como la mascara era "todo", Ekkar los pisaba y podia saltar
    /// sin parar mientras estuviera encima de uno.
    ///
    /// Y hay una ventana de gracia justo despues de despegar en la que se da
    /// por hecho que esta en el aire aunque el circulo del pie siga rozando el
    /// suelo. Sin ella la animacion iba y venia entre correr y saltar durante
    /// dos o tres fotogramas, que es el tiron que se veia al saltar, y encima
    /// el contador de saltos se reiniciaba y se comia el salto doble.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class EkkarController : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] float moveSpeed = 5.5f;
        [SerializeField] float acceleration = 45f;
        [SerializeField] float jumpVelocity = 13.5f;
        [SerializeField] float gravityScale = 4.2f;
        [SerializeField] float fallMultiplier = 1.6f;

        [Header("Salto")]
        [SerializeField] int saltosMaximos = 2;
        [SerializeField] float coyote = 0.11f;        // margen tras dejar el borde
        [SerializeField] float bufferSalto = 0.12f;   // margen si se pulsa antes de aterrizar
        [Tooltip("Segundos tras despegar en los que se ignora el suelo.")]
        [SerializeField] float graciaDespegue = 0.14f;

        [Header("Dash")]
        [SerializeField] float dashSpeed = 17f;
        [SerializeField] float dashDuration = 0.26f;
        [SerializeField] float dashCooldown = 0.7f;
        [SerializeField] int manaDash = 1;
        [Tooltip("Dano que hace al atravesar a alguien. 0 = solo sirve para moverse.")]
        [SerializeField] int danoDash = 1;
        [SerializeField] float anchoDash = 1.1f;

        [Header("Tormenta del salto doble")]
        [Tooltip("Hasta donde muerde el remolino de rayos.")]
        [SerializeField] float radioTormenta = 3.2f;
        [SerializeField] int danoTormenta = 1;
        [SerializeField] float enfriamientoTormenta = 1.4f;
        [Tooltip("La tormenta es una habilidad, no un regalo del salto doble: se paga.")]
        [SerializeField] int manaTormenta = 2;

        [Header("Ataque")]
        [SerializeField] float attackDuration = 0.42f;
        [SerializeField] float duracionHabilidad = 1.1f;

        [Header("Habilidades")]
        [SerializeField] float segundosDetenido = 4f;
        [SerializeField] int manaDetener = 3;
        [SerializeField] float enfriamientoDetener = 9f;
        [SerializeField] float radioChrono = 5.5f;
        [SerializeField] int manaChrono = 6;
        [SerializeField] float enfriamientoChrono = 16f;

        [Header("Cuando sale el efecto")]
        [Tooltip("Que parte de la animacion pasa antes de que el conjuro surta " +
                 "efecto. Con 0 sale a la vez que se pulsa, que es lo que hacia " +
                 "que no se entendiera de donde venia el dano.")]
        [SerializeField, Range(0f, 1f)] float momentoDetener = 0.55f;
        [Tooltip("En el chronobreak es cuando la espada toca el suelo.")]
        [SerializeField, Range(0f, 1f)] float momentoChrono = 0.62f;
        [Tooltip("Lo que dura la embestida con la que arranca el chronobreak. " +
                 "Calibrada para que recorra algo menos que un dash: 0,18 del " +
                 "clip a 8 por segundo son unas 4 unidades.")]
        [SerializeField, Range(0f, 1f)] float embestidaChrono = 0.18f;
        [SerializeField] float velocidadEmbestida = 8f;
        [Tooltip("Segundos sin pelear antes de envainar la espada solo. 0 = nunca.")]
        [SerializeField] float envainaTras = 4f;

        [Header("Suelo")]
        [SerializeField] Transform groundCheck;
        [SerializeField] float groundRadius = 0.2f;
        [SerializeField] LayerMask groundMask = ~0;

        Rigidbody2D _rb;
        Animator _anim;
        SpriteRenderer _sr;
        PlayerCombat _combate;

        bool _grounded;
        int _facing = 1;
        int _saltos;
        float _lockTimer;
        float _dashTimer, _dashCd;
        float _detenerCd, _chronoCd, _tormentaCd;
        // conjuros lanzados que todavia no han salido: primero el gesto
        float _detenerEn = -1f, _chronoEn = -1f, _embisteHasta = -1f;
        float _enSuelo, _pidioSalto, _despegoEn = -99f;
        bool _espadaFuera = true;
        bool _estabaEnAire;
        float _proximoPaso;
        float _ultimoCombate;
        string _state = "";

        // hasta cuando no se puede pisar la animacion en curso con un idle
        float _animHasta;
        readonly System.Collections.Generic.Dictionary<string, float> _duraciones =
            new System.Collections.Generic.Dictionary<string, float>();

        public bool EnSuelo => _grounded;
        public bool Ocupado => _lockTimer > 0f;

        /// <summary>Altura del pecho, de donde salen los golpes.</summary>
        Vector2 Pecho => (Vector2)transform.position + new Vector2(0f, 1.05f);

        // lo lee la barra de habilidades para pintar el enfriamiento
        public float DashCooldown => dashCooldown;
        public float DashRestante => Mathf.Max(0f, _dashCd);
        public float DetenerCooldown => enfriamientoDetener;
        public float DetenerRestante => Mathf.Max(0f, _detenerCd);
        public float ChronoCooldown => enfriamientoChrono;
        public float ChronoRestante => Mathf.Max(0f, _chronoCd);
        public int ManaDash => manaDash;
        public int ManaDetener => manaDetener;
        public int ManaChrono => manaChrono;
        public int ManaTormenta => manaTormenta;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _anim = GetComponentInChildren<Animator>();
            _sr = GetComponentInChildren<SpriteRenderer>();
            _combate = GetComponent<PlayerCombat>();

            _rb.gravityScale = gravityScale;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            MideAnimaciones();
        }

        /// <summary>
        /// Apunta cuanto dura cada animacion.
        ///
        /// Los tiempos estaban escritos a mano y no se parecian a los clips:
        /// envainar dura casi dos segundos y se cortaba a los 0,5. De ahi que
        /// Ekkar se viera "cortado" todo el rato. Ahora la duracion sale del
        /// propio clip, asi que si manana el arte cambia, esto se entera solo.
        /// </summary>
        void MideAnimaciones()
        {
            if (_anim == null || _anim.runtimeAnimatorController == null) return;
            foreach (var clip in _anim.runtimeAnimatorController.animationClips)
                if (clip != null) _duraciones[clip.name] = clip.length;
        }

        float Dura(string estado, float porDefecto = 0.5f)
            => _duraciones.TryGetValue(estado, out float d) && d > 0.01f ? d : porDefecto;

        /// <summary>
        /// Lanza la animacion y la protege hasta que termina. Devuelve lo que
        /// dura, para que quien la pide decida cuanto tiempo quita el control:
        /// no es lo mismo el dibujo que el bloqueo.
        /// </summary>
        float Lanza(string estado, float porDefecto = 0.5f)
        {
            Play(estado, true);
            float largo = Dura(estado, porDefecto);
            _animHasta = Time.time + largo;
            return largo;
        }

        void Update()
        {
            // con el menu abierto el teclado es suyo: si no, saltar para
            // moverte por el menu tambien hacia saltar a Ekkar por detras
            if (UI.PauseMenu.Abierta) return;

            float dt = Time.deltaTime;
            if (_lockTimer > 0f) _lockTimer -= dt;
            if (_dashCd > 0f) _dashCd -= dt;
            if (_detenerCd > 0f) _detenerCd -= dt;
            if (_chronoCd > 0f) _chronoCd -= dt;

            MiraSuelo();
            ResuelveConjuros(dt);

            float move = ReadMove();

            // ---- dash en curso: manda sobre todo lo demas
            if (_dashTimer > 0f)
            {
                _dashTimer -= dt;
                _rb.linearVelocity = Vector2.zero;
                transform.position += new Vector3(_facing * dashSpeed * dt, 0f, 0f);

                // el dash embiste: cuesta mana, asi que tiene que servir para
                // algo mas que para desplazarse. Cada bicho solo lo encaja una
                // vez, que de eso ya se ocupa su margen de invulnerabilidad
                if (danoDash > 0)
                    Damageable.GolpearFrente(Pecho, _facing, anchoDash, 1.0f, 1.0f,
                                             Damageable.Bando.Jugador, danoDash);

                if (_dashTimer <= 0f) _rb.gravityScale = gravityScale;
                return;
            }

            if (Mathf.Abs(move) > 0.01f && _lockTimer <= 0f)
            {
                _facing = move > 0f ? 1 : -1;
                if (_sr != null) _sr.flipX = _facing < 0;
            }

            float targetX = (_lockTimer > 0f ? 0f : move) * moveSpeed;
            float vx = Mathf.MoveTowards(_rb.linearVelocity.x, targetX, acceleration * dt);
            _rb.linearVelocity = new Vector2(vx, _rb.linearVelocity.y);

            // caer mas rapido que subir: el salto se siente mejor
            if (_rb.linearVelocity.y < 0f)
                _rb.linearVelocity += Vector2.up * Physics2D.gravity.y * gravityScale * (fallMultiplier - 1f) * dt;

            Salto(dt);

            if (Habilidades()) return;

            // guarda la espada sola cuando lleva un rato sin pelear, y la saca
            // al primer golpe. La tecla Q sigue funcionando para quien quiera
            // hacerlo a mano.
            if (_espadaFuera && envainaTras > 0.01f &&
                _lockTimer <= 0f && _grounded && Mathf.Abs(vx) < 0.2f &&
                Time.time - _ultimoCombate > envainaTras)
            {
                _espadaFuera = false;
                Lanza("envainar", 1.9f);
                _lockTimer = 0.3f;
                Audio.Sfx.Play("espada_out", 0.5f, 0.85f);
                return;
            }

            if (_lockTimer > 0f) return;

            // Mientras dure un golpe, andar o saltar no le pisan el dibujo. Era
            // lo que cortaba el ataque en salto a mitad: el control vuelve antes
            // que la animacion, y "salto_normal" entraba encima. Asi Ekkar salta
            // una vez y, si el jugador pulsa J en el aire, el golpe se ve entero
            // y despues vuelve solo a la caida.
            bool golpeEnCurso = Time.time < _animHasta && EsGolpe(_state);

            if (!_grounded)
            {
                if (!golpeEnCurso) Play(_saltos > 1 ? "salto_doble" : "salto_normal");
            }
            else if (Mathf.Abs(vx) > 0.4f)
            {
                if (!golpeEnCurso) Play("run");
                // un paso cada dos zancadas: marca el ritmo sin taladrar
                if (Time.time >= _proximoPaso)
                {
                    _proximoPaso = Time.time + 0.29f;
                    Audio.Sfx.Play("paso", 0.5f);
                }
            }
            // quieto: se deja acabar lo que estuviera sonando antes de volver al
            // idle. Moverse o saltar si lo corta, que para eso es tu orden
            else if (Time.time >= _animHasta) Play("idle");
        }

        // ------------------------------------------------------------- suelo

        void MiraSuelo()
        {
            // recien despegado: aunque el pie siga rozando el suelo, esta en
            // el aire. Es lo que evita el parpadeo de animacion y que se pierda
            // el salto doble nada mas empezar a subir.
            if (Time.time - _despegoEn < graciaDespegue)
            {
                _grounded = false;
                _estabaEnAire = true;
                return;
            }

            _grounded = false;
            if (groundCheck == null) return;

            var tocados = Physics2D.OverlapCircleAll(groundCheck.position, groundRadius, groundMask);
            foreach (var col in tocados)
            {
                if (col == null || col.isTrigger) continue;          // enemigos, metas, zonas
                if (col.transform.IsChildOf(transform)) continue;    // su propio cuerpo
                _grounded = true;
                break;
            }

            if (_grounded)
            {
                if (_estabaEnAire) Audio.Sfx.Play("aterrizar", 0.75f);
                _estabaEnAire = false;
                _enSuelo = Time.time;
                if (_rb.linearVelocity.y <= 0.01f) _saltos = 0;
            }
            else _estabaEnAire = true;
        }

        void Salto(float dt)
        {
            if (JumpPressed()) _pidioSalto = Time.time;

            bool quiere = Time.time - _pidioSalto <= bufferSalto;
            bool puedeDesdeSuelo = Time.time - _enSuelo <= coyote && _saltos == 0;
            bool leQuedanSaltos = _saltos < saltosMaximos;

            if (!quiere || _lockTimer > 0f || !leQuedanSaltos) return;
            if (!puedeDesdeSuelo && _saltos == 0) return;   // caida libre: no regala el primero

            _pidioSalto = -99f;
            _saltos++;
            _despegoEn = Time.time;
            _grounded = false;
            _estabaEnAire = true;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpVelocity);
            Lanza(_saltos > 1 ? "salto_doble" : "salto_normal", 0.85f);
            Audio.Sfx.Play("salto", 0.7f, _saltos > 1 ? 1.25f : 1f);

            // El salto doble se lleva consigo un tornado de rayos que muerde a
            // lo que tenga alrededor. Es una habilidad por derecho propio y se
            // paga, pero SOLO si hay a quien morder.
            //
            // Cobrarla en cada salto doble vaciaba la barra sola: el salto doble
            // se usa para moverse todo el rato, y el mana solo entra acertando
            // golpes, asi que Ekkar pagaba por recorrer el nivel. Ahora en campo
            // abierto el salto doble es gratis y no sale la tormenta; al lado de
            // un enemigo sale y se cobra. El Paga va el ultimo a proposito, que
            // gasta mana al llamarlo.
            if (_saltos > 1 && Time.time >= _tormentaCd &&
                Damageable.HayObjetivo(transform.position + Vector3.up, radioTormenta,
                                       Damageable.Bando.Jugador) &&
                _combate != null && _combate.Paga(manaTormenta))
            {
                _tormentaCd = Time.time + enfriamientoTormenta;
                FX.StormBurst.Lanza(transform, radioTormenta, danoTormenta);
                Audio.Sfx.Play("chrono", 0.55f, 1.6f);
            }
        }

        // -------------------------------------------------------- habilidades

        bool Habilidades()
        {
            if (_lockTimer > 0f) return false;

            if (DashPressed())
            {
                if (_dashCd > 0f) return false;
                // el dash es una habilidad, y las habilidades se pagan
                if (_combate != null && !_combate.Paga(manaDash)) return false;

                _dashTimer = dashDuration;
                _dashCd = dashCooldown;
                _lockTimer = dashDuration;
                _rb.gravityScale = 0f;
                Lanza("dash", 1.5f);
                Audio.Sfx.Play("dash", 0.9f);
                return true;
            }

            bool cargar = CargadoPulsado();
            if (AttackPressed() || cargar)
            {
                bool cargado = cargar;
                if (_combate != null && !_combate.LanzarGolpe(cargado)) return false;

                string golpe;
                if (cargado && _combate != null) golpe = _combate.AnimacionCargada(_grounded);
                else if (!_grounded) golpe = "salto_autoataque_vertical";
                else if (ArribaPulsado()) golpe = "ataque_vertical";
                else golpe = "ataque_horizontal";

                // el dibujo va entero, pero el control vuelve a media estocada:
                // bloquear los 0,9s que dura el clip hace la pelea de barro
                float largo = Lanza(golpe, attackDuration);
                _lockTimer = Mathf.Clamp(largo * 0.55f, 0.26f, 0.62f);

                Audio.Sfx.Play(cargado ? "mana" : "espada", cargado ? 1f : 0.8f);
                Desenvaina();
                return true;
            }

            if (DetenerPulsado())
            {
                if (_detenerCd > 0f) return false;
                if (_combate != null && !_combate.Paga(manaDetener)) return false;

                // El tiempo no se para en el fotograma en que pulsas: Ekkar hace
                // el gesto y despues sale. El bloqueo dura lo que el gesto, y ni
                // uno mas: la gracia de parar el tiempo es poder moverte mientras
                // el resto no puede.
                _detenerCd = enfriamientoDetener;
                float largoDetener = Lanza("detener_tiempo", 1.9f);
                _detenerEn = Time.time + largoDetener * momentoDetener;
                _lockTimer = largoDetener * momentoDetener;
                Audio.Sfx.Play("detener", 0.7f, 1.3f);   // el conjuro al empezar
                return true;
            }

            if (ChronoPulsado())
            {
                if (_chronoCd > 0f) return false;
                if (_combate != null && !_combate.Paga(manaChrono)) return false;

                // El chronobreak tiene tres tiempos: Ekkar embiste, clava la
                // espada en el suelo, y entonces la energia del tiempo revienta
                // a su alrededor. Antes salia todo junto en el primer fotograma
                // y parecia un manotazo.
                _chronoCd = enfriamientoChrono;
                float largoChrono = Lanza("chronobreak", duracionHabilidad * 1.6f);
                _embisteHasta = Time.time + largoChrono * embestidaChrono;
                _chronoEn = Time.time + largoChrono * momentoChrono;
                _lockTimer = largoChrono * momentoChrono;
                Audio.Sfx.Play("chrono", 1f);
                return true;
            }

            if (EspadaPulsada())
            {
                _espadaFuera = !_espadaFuera;

                // el clip dura casi dos segundos: se deja correr entero, pero
                // el control vuelve enseguida
                Lanza(_espadaFuera ? "desenvainar" : "envainar", 1.9f);
                _lockTimer = 0.3f;
                Audio.Sfx.Play("espada_out", 0.8f);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Saca lo que se lanzo hace un momento y todavia no habia salido.
        ///
        /// Los conjuros se piden en un fotograma pero surten efecto mas tarde,
        /// cuando la animacion llega a su momento. Sin esto, pulsabas E y el
        /// mundo se congelaba antes de que a Ekkar le diera tiempo a levantar
        /// la mano, y no habia forma de leer de donde venia nada.
        /// </summary>
        void ResuelveConjuros(float dt)
        {
            // ---- la embestida con la que arranca el chronobreak
            if (_embisteHasta > 0f)
            {
                if (Time.time < _embisteHasta)
                {
                    transform.position += new Vector3(_facing * velocidadEmbestida * dt, 0f, 0f);
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                }
                else _embisteHasta = -1f;
            }

            // ---- detener el tiempo
            if (_detenerEn > 0f && Time.time >= _detenerEn)
            {
                _detenerEn = -1f;
                TimeControl.Detener(segundosDetenido);
                Audio.Sfx.Play("detener", 1f);
            }

            // ---- el chronobreak: la espada toca el suelo y revienta
            if (_chronoEn > 0f && Time.time >= _chronoEn)
            {
                _chronoEn = -1f;

                // la explosion de rayos es solo lo que se ve: el dano lo reparte
                // Chronobreak, que es quien sabe a quien deshuesa y a quien no
                FX.StormBurst.Lanza(transform, radioChrono, 0, 1.5f, 1);
                Audio.Sfx.Play("impacto", 1f, 0.7f);

                // no arrasa: deshuesa. Deja a todo el mundo en las ultimas y
                // solo remata a quien ya venia herido
                var (caidos, tocados) = Damageable.Chronobreak(
                    transform.position + Vector3.up, radioChrono, Damageable.Bando.Jugador);

                var oro = new Color(0.98f, 0.75f, 0.14f);
                UI.PlayerHealthBar.Aviso(
                    caidos > 0 ? $"CHRONOBREAK   {caidos} CAEN" :
                    tocados > 0 ? "CHRONOBREAK   EN LOS HUESOS" : "CHRONOBREAK", oro);
            }
        }

        /// <summary>Saca la espada si estaba guardada, al empezar a pelear.</summary>
        void Desenvaina()
        {
            _ultimoCombate = Time.time;
            if (_espadaFuera) return;
            _espadaFuera = true;
            Audio.Sfx.Play("espada_out", 0.7f);
        }

        // ------------------------------------------------------------ animar

        /// <summary>Si ese estado es un golpe, y por tanto no se le pisa.</summary>
        static bool EsGolpe(string estado)
            => !string.IsNullOrEmpty(estado)
               && (estado.StartsWith("ataque")
                   || estado.StartsWith("autoataque")
                   || estado.StartsWith("salto_autoataque")
                   || estado.Contains("carga_mana"));

        void Play(string state, bool forzar = false)
        {
            if (_anim == null || (!forzar && _state == state)) return;
            if (_anim.runtimeAnimatorController == null) return;
            if (!_anim.HasState(0, Animator.StringToHash(state)))
            {
                Debug.LogWarning($"[Ekkar] Falta el estado '{state}' en el controlador de Ekkar", this);
                return;
            }
            _state = state;
            _anim.Play(state, 0, 0f);
        }

        // ------------------------------------------------------------ entrada

        static float ReadMove()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return 0f;
            float v = 0f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) v -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) v += 1f;
            return v;
#else
            return Input.GetAxisRaw("Horizontal");
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static bool Tecla(System.Func<Keyboard, UnityEngine.InputSystem.Controls.KeyControl> sel)
        {
            var k = Keyboard.current;
            return k != null && sel(k).wasPressedThisFrame;
        }
        static bool JumpPressed()   => Tecla(k => k.spaceKey) || Tecla(k => k.wKey);
        static bool DashPressed()   => Tecla(k => k.leftShiftKey);
        static bool AttackPressed() => Tecla(k => k.jKey);
        static bool CargadoPulsado()=> Tecla(k => k.kKey);
        static bool DetenerPulsado()=> Tecla(k => k.eKey);
        static bool ChronoPulsado() => Tecla(k => k.rKey);
        static bool EspadaPulsada() => Tecla(k => k.qKey);
        static bool ArribaPulsado()
        {
            var k = Keyboard.current;
            return k != null && (k.iKey.isPressed || k.upArrowKey.isPressed);
        }
#else
        static bool JumpPressed()   => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W);
        static bool DashPressed()   => Input.GetKeyDown(KeyCode.LeftShift);
        static bool AttackPressed() => Input.GetKeyDown(KeyCode.J);
        static bool CargadoPulsado()=> Input.GetKeyDown(KeyCode.K);
        static bool DetenerPulsado()=> Input.GetKeyDown(KeyCode.E);
        static bool ChronoPulsado() => Input.GetKeyDown(KeyCode.R);
        static bool EspadaPulsada() => Input.GetKeyDown(KeyCode.Q);
        static bool ArribaPulsado() => Input.GetKey(KeyCode.I) || Input.GetKey(KeyCode.UpArrow);
#endif

        void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = Palette.CyanBright;
            Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        }
    }
}
