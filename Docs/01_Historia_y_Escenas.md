# Guerrero del Tiempo — Historia y estructura de escenas

## 1. Premisa

En el centro del mundo late el **Gran Reloj**: una máquina que no mide el tiempo,
sino que lo *sostiene*. Mientras sus tres piezas giran, las eras se suceden en
orden. La **Orden del Engranaje** lo custodia desde su torre. Ekkar es su
caballero más joven.

El **Señor del Tiempo** fue el primer guardián del Gran Reloj. Durante siglos vio
cómo cada era terminaba igual: en fuego, en humo, en ruina. Y llegó a una
conclusión terrible pero coherente — *si un momento nunca termina, la catástrofe
nunca ocurre*.

Así que rompió el Gran Reloj.

No para destruir el tiempo, sino para **congelar cada era en el instante justo
antes de su final**. El castillo que iba a caer nunca termina de caer. La
fundición que iba a explotar nunca termina de explotar. La ciudad que iba a
apagarse nunca termina de apagarse. Nadie muere. Nadie vive. Todos repiten su
peor hora para siempre.

Ekkar sobrevivió porque estaba **dentro del núcleo del Reloj** cuando se rompió.
Por eso el tiempo le obedece a él y no al revés: puede **detenerlo** (E) y puede
**romperlo** (R, *Chronobreak*).

Las tres piezas del Gran Reloj cayeron cada una en una era. Ekkar debe
recuperarlas y volver a montarlo.

### El dilema

Reparar el Gran Reloj significa **dejar que cada era termine su hora**. El
castillo caerá. La gente que lleva siglos repitiendo su última noche morirá de
verdad. Ekkar no está salvando a nadie: está devolviéndoles la muerte que les
robaron. Ese es el precio, y el juego no lo esconde.

---

## 2. Personajes canónicos

Las referencias de diseño están en `Tools/refs/`. **Ninguna imagen generada se
acepta si no coincide con ellas.**

### Ekkar — el Guerrero del Tiempo
`Tools/refs/ekkar.png`

Armadura de placas plateada bruñida sobre cota de malla, capa roja intensa,
bufanda/gorjal rojo, gema azul circular en el pecho, cinturón marrón con hebilla
cuadrada, pelo castaño despeinado, ojos azules, espada de hoja marrón-cobriza al
costado. Joven, no barbudo, expresión seria y contenida.

**Descartar** cualquier imagen donde: lleve armadura oscura o dorada, tenga
capucha, tenga pelo rubio o blanco, tenga barba, lleve casco cerrado, o tenga
proporciones distintas (el "Ekkar parecido a Ekko de LoL" del archivo
`skin_ekkar_parecido_a_ekko_lol.png` **no** es canónico).

### El Señor del Tiempo
`Tools/refs/senor_del_tiempo.png` — y, para generar vídeo, las dos versiones
con fondo croma en
`Tools/prompts/hora_cero/enemigos/senor_del_tiempo/videos/`:
`senor_del_tiempo_verde_fondo.png` (con el portal) y
`senor_del_tiempo_fondo_verde_sin_portal.png` (sin él).

Figura humanoide acorazada que **flota**, de frente. Armadura pesada de placas
azul acero oscuro con filos dorados y luz cian ardiendo en las juntas y en el
pecho. Yelmo de cuernos y, debajo, una cara pálida de calavera con la boca
abierta llena de dientes y dos ojos de fuego cian. Hombreras enormes.

**Tiene dos brazos y dos piernas.** Los brazos son largos y segmentados y
acaban en garras; las piernas cuelgan algo dobladas porque nunca toca el suelo.

Lo que se mueve a su alrededor **no son extremidades**, y confundirlo es lo que
arruinaba los prompts:

- **Tentáculos oscuros de su propia piel estirada**, que le salen de la cabeza,
  los hombros, la espalda y las piernas. Se alargan y se enroscan: es él
  deformándose. El tiempo lo está descosiendo y así se ve.
- **Cintas doradas** anchas y planas con el borde cian claro, dando vueltas por
  el aire, y **engranajes de oro** de varios tamaños girando en órbita.

Detrás, en la versión con portal, un disco azul enorme de energía en espiral.

**Fase 2:** el mismo cuerpo y la misma silueta. Cambia la superficie: armadura
negra carbón con grabado dorado encendido, ojos y boca **naranjas** en vez de
cian, una columna de fuego naranja saliéndole del pecho y bajándole por el
vientre, y relámpagos **violeta** en las manos. Sin portal.

**Descartar** cualquier imagen donde tenga más de dos brazos, donde los
tentáculos se conviertan en alas o en brazos, o donde le cambien la silueta.

---

## 3. Las cinco escenas

| # | Escena Unity | Nombre en ficción | Qué es |
|---|---|---|---|
| 0 | `MainMenu` | **La Torre Quebrada** | El instante en que el Gran Reloj estalla. Es el menú. |
| 1 | `01_EdadMedia_SitioEterno` | **El Sitio Eterno** | Un reino en su última noche, repetida para siempre. |
| 2 | `02_EraIndustrial_FundicionDeLasHoras` | **La Fundición de las Horas** | Una fábrica que forja horas en lugar de acero. |
| 3 | `03_FuturoDigital_NeonSinManana` | **Neón Sin Mañana** | Una ciudad que ya no puede guardar el día siguiente. |
| 4 | `04_HoraCero_VacioEntreSegundos` | **La Hora Cero** | El vacío entre segundos. Ahí espera el Señor del Tiempo. |

### Las tres piezas del Gran Reloj

Cada era guarda la pieza que corresponde a **cómo esa época medía el tiempo**:

- **El Badajo** — Edad Media. Dentro de la campana que nunca termina de sonar.
- **El Volante** — Era Industrial. El escape mecánico, en el corazón de la fundición.
- **El Cuarzo** — Futuro Digital. El oscilador, en el servidor central de la ciudad.

Campana → escape mecánico → cristal de cuarzo. Es la historia real de la
relojería, y es la columna vertebral del juego.

---

## 4. El combate final — La Hora Cero

El Señor del Tiempo no pelea en un solo sitio. Cuando Ekkar monta el Gran Reloj,
las tres eras se derrumban unas sobre otras, y la pelea **atraviesa las tres**
antes de caer al vacío. Una sola escena, cuatro fases, el escenario se
transforma alrededor:

- **Fase I — Eco de Ceniza.** La arena se viste de piedra y estandartes rojos.
  Él invoca ecos del asedio. La luna sangrienta al fondo.
- **Fase II — Eco de Hierro.** El suelo se convierte en pasarelas y calderas.
  Vapor, pistones, engranajes gigantes que atraviesan la arena.
- **Fase III — Eco de Neón.** Rascacielos y hologramas rotos. Él se fragmenta en
  copias digitales de sí mismo.
- **Fase Final — Hora Cero.** Todo se apaga. No hay suelo, no hay cielo, no hay
  color salvo cian y oro sobre negro absoluto. Aquí el Señor del Tiempo pelea
  sin aura: solo él y Ekkar, y ninguno de los dos envejece un segundo.

---

## 5. Nivel 1 — El Sitio Eterno (Edad Media)

> El reino de **Valdheim** cayó hace seiscientos años. Todavía está cayendo.

La luna está a mitad de un eclipse y no avanza. Las flechas cuelgan en el aire.
El humo sube y se detiene. Los soldados mueren y se levantan y vuelven a morir,
y ninguno recuerda por qué. En lo alto del torreón, la campana lleva seis siglos
a mitad de campanada.

### Tramos

1. **El Campo de Ceniza** — Llegada. Cementerio y campo de batalla fuera de las
   murallas. Enseña moverse, saltar, atacar. Primeros esqueletos.
2. **La Brecha** — Ascenso vertical por la muralla rota. Espectros que atraviesan
   la piedra. Escaleras de asedio como plataformas.
3. **El Patio de Armas** — Arena de combate. Cada 8 segundos el bucle temporal
   se reinicia y una andanada de catapulta cruza el patio. Con **E** las rocas
   se congelan y se convierten en plataformas: la única forma de cruzar.
4. **La Escalera de Fuego** — Interior del torreón en llamas, ascenso rápido.
5. **La Campana** — Mini-jefe: **el Caballero Ceniciento**, campeón de Valdheim.
   Al vencerlo, Ekkar arranca **el Badajo**. La campana termina su campanada, el
   castillo termina de caer, y Ekkar tiene que escapar mientras el tramo entero
   se derrumba por primera vez en seiscientos años.

### Enemigos

- **Soldado de hueso** — infantería básica, ataque cuerpo a cuerpo lento.
- **Espectro de asedio** — vuela, atraviesa paredes, persigue.
- **Caballero Ceniciento** — mini-jefe. Espejo oscuro de Ekkar: misma silueta,
  armadura quemada, mandoble roto cuyos fragmentos flotan congelados.

### El uso del tiempo como mecánica

Todo el nivel enseña una idea: **lo que está congelado es sólido**.

- Flechas y rocas detenidas = plataformas.
- Un rastrillo cayendo = techo si lo congelas a media caída.
- Una explosión detenida = muro que te protege.
- El Chronobreak (R) rompe lo congelado y libera toda la energía acumulada.

---

## 6. Animaciones de fondo (lo que hace que la escena esté "viva")

El fondo no es un cuadro: es un mundo atascado. Cada efecto refuerza eso.

| Efecto | Qué hace |
|---|---|
| **Parallax de 5 capas** | Cielo, horizonte, castillo, suelo, primer plano. |
| **Eclipse lentísimo** | La luna es lo único del cielo que se mueve, casi imperceptible. |
| **Ceniza suspendida** | Partículas que caen, se detienen a media caída y quedan colgadas. |
| **Tartamudeo temporal** | Cada ~12 s el fondo entero retrocede 0,4 s y repite. La firma visual del juego. |
| **Estandartes** | Ondean 3 fotogramas y se congelan de golpe. |
| **Grietas de cielo** | Vetas cian que laten en la bóveda como cristal roto. |
| **Relámpago silueta** | Destello que recorta el castillo en negro contra el cielo. |
| **Andanadas congeladas** | Rocas de catapulta cruzando el fondo en cámara ultra lenta. |
| **Modo Detener Tiempo** | Al pulsar E: el fondo se dessatura a cian, todo se para, anillo expansivo. |

---

## 7. Estado del arte (auditoría)

### Lo que sirve tal cual
- `Tools/refs/ekkar.png` — 16 fotogramas de idle ya importados y alineados.
- `Tools/refs/senor_del_tiempo.png` — diseño del jefe final, listo.
- Las ilustraciones de `ekkar/sin hacer/excenarios/` — **referencia de estilo**,
  no assets finales. Marcan el listón: panorámicas anchas, muy detalladas,
  luz dramática.

### Lo que NO encaja y por qué

**Gothicvania Cemetery** está dibujado a otra escala. Sus enemigos miden 44×52 px
y sus tiles 32 px; Ekkar mide 400×618. Puestos juntos, o Ekkar parece un gigante
o los esqueletos parecen bloques de LEGO. No es un problema de gusto: es una
diferencia de densidad de píxel de casi 10×.

→ **Decisión:** generar los enemigos medievales al tamaño y estilo de Ekkar.
Gothicvania se reserva para siluetas de fondo lejano, donde la diferencia de
resolución se lee como profundidad y no como error.

**`escenario_edad_media/*.png`** es pintura digital, no pixel art. Precioso, pero
de otro juego. Se sustituye.
