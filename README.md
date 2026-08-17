# Guerrero del Tiempo

Juego de plataformas y acción 2D en pixel art, para PC. Cuatro eras congeladas
en el instante anterior a su desastre, y un guardián que tiene que decidir si
las deja terminar.

Desarrollado con **Unity 6.5** (6000.5.5f1) y URP 2D, a 1920 × 1080.

---

## Descargar y jugar

El juego compilado está en [`distrib/windows-x64/`](distrib/windows-x64).
Se descarga la carpeta entera y se ejecuta **`GuerreroDelTiempo.exe`**.

No hace falta instalar nada más: el motor va dentro de la compilación. No
necesita Unity, ni tiempo de ejecución aparte, ni conexión a internet.

**Requisitos:** Windows 10 o superior de 64 bits, 4 GB de RAM, gráfica
compatible con DirectX 11 y 700 MB libres.

> Los archivos de más de 100 MB viajan por **Git LFS**. Si al clonar aparecen
> como archivos de texto de pocos bytes, ejecuta `git lfs install && git lfs pull`.

---

## La historia

El **Señor del Tiempo** no destruyó el tiempo: lo detuvo. Fue el primer guardián
del Gran Reloj y, después de ver a cada era terminar en ruina, lo rompió para
congelar cada época en el instante anterior a su desastre. Nadie muere y nadie
vive: todos repiten su peor hora. Es una forma de piedad que se volvió condena.

**Ekkar** sobrevivió porque estaba dentro del núcleo del Reloj cuando se rompió.
Tiene que recuperar las tres piezas que faltan y repararlo, y eso significa
**dejar que cada era termine de morir**. El jugador no salva el mundo: decide
que el mundo pueda acabarse, que es la única manera de que vuelva a empezar.

Las tres piezas siguen la historia real de la relojería, y ese es el hilo que
une las cuatro eras: cada una aporta el mecanismo que su época inventó para
medir el tiempo.

| Pieza | Era | Qué es |
|---|---|---|
| El Badajo | Edad Media | La campana: la primera medida comunitaria del tiempo |
| El Volante | Era Industrial | El escape mecánico: el tiempo se vuelve portátil |
| El Cuarzo | Futuro Digital | El oscilador: el tiempo se vuelve frecuencia |
| El Núcleo | Hora Cero | Las tres reunidas: el tiempo vuelve a correr |

---

## Las cuatro eras

| Era | En la ficción | El instante congelado |
|---|---|---|
| **Edad Media** | El Sitio Eterno | El asedio, un segundo antes de que la muralla ceda |
| **Era Industrial** | La Fundición de las Horas | La caldera, a punto de estallar desde hace un siglo |
| **Futuro Digital** | Neón Sin Mañana | Los servidores, en su último ciclo antes del apagón |
| **Hora Cero** | El Vacío Entre Segundos | No es un instante: es la ausencia de instante |

Las tres primeras son recorridos laterales de 140 unidades que terminan en el
campeón de la era. La Hora Cero es una arena con los telones de las tres eras
colgados uno al lado del otro, porque no es un lugar sino la falta de lugar.

---

## Cómo se juega

| Tecla | Acción |
|---|---|
| `A` / `D` | Caminar |
| `Espacio` | Saltar. Pulsado dos veces, salto doble |
| `J` | Ataque con la espada, encadenable en tres golpes |
| `W` + `J` | Ataque hacia arriba, el único que alcanza a los voladores |
| `K` | Golpe cargado: más daño, y devuelve vida al acertar |
| `Shift` | Impulso, con invulnerabilidad durante el recorrido |
| `Q` | Detener el tiempo |
| `E` | Chronobreak |
| `Esc` | Menú de pausa |

### La regla que sostiene el combate

**El maná solo sube al acertar un golpe.** No se recoge del suelo, no se recarga
con el paso del tiempo y fallar no suma nada. Cada golpe acertado le arranca a
la era un fragmento del segundo que tiene detenido.

Eso significa que para poder detener el tiempo hay que haber peleado antes. Un
jugador que evita el combate llega al campeón de la era sin recurso, y es a
propósito.

### Las cuatro habilidades

| Habilidad | Coste | Enfriamiento | Qué hace |
|---|---|---|---|
| Impulso | 2 de maná | 1,2 s | Desplazamiento rápido con invulnerabilidad; embiste y daña |
| Detener el tiempo | 6 de maná | 12 s | Congela enemigos, proyectiles y plataformas durante 5 s |
| Chronobreak | 10 de maná | 20 s | Reduce la vida en área y remata a quien esté por debajo del 20 % |
| Tormenta de rayos | 2 de maná | 3 s | Sale del salto doble. Solo cuesta si hay un enemigo en el radio |

Con la barra llena caben dos detenciones del tiempo, o un Chronobreak y dos
impulsos. Nunca caben dos Chronobreak: es lo que impide resolver un combate de
jefe repitiendo la habilidad definitiva.

---

## Qué hay dentro

- **562 fotogramas** repartidos en 21 animaciones del personaje
- **15 enemigos**: infantería, a distancia y voladores por era, más cuatro
  campeones y el jefe final por fases
- **Cuatro niveles** construidos por código, con parallax de cinco capas,
  hogueras de guardado y música propia
- **Todo el audio generado por síntesis**, sin un solo archivo de sonido en el
  proyecto: música de cada era, tema del jefe en dos versiones y efectos
- **Menú completo**: parallax, título letra a letra, opciones, créditos,
  confirmación de salida y menú de pausa
- **Cinemática de apertura** montada en el motor, no como vídeo pregrabado

---

## Ver el juego en marcha

| Vídeo | Qué muestra |
|---|---|
| [Cinemática de apertura](https://youtu.be/xqbQkjwksUg) | Por qué el tiempo está detenido |
| [Movimiento y habilidades](https://youtu.be/DaCOtpI6_AQ) | El recorrido y las cuatro habilidades |
| [Menú y combate](https://youtu.be/bT37eldsww4) | El menú animado y la pelea |
| [Carrera por el Futuro Digital](https://youtu.be/fKFcbJl-RLs) | El nivel más oscuro del juego |
| [Partida completa: Era Industrial](https://youtu.be/mh4FII0RP48) | Un nivel entero hasta vencer al campeón |
| [El objeto valioso](https://youtu.be/H1LEot6k1uk) | Ekkar obtiene el Núcleo del Gran Reloj |
| [Tráiler](https://youtu.be/5pWx5J2K7mc) | Resumen de todo el desarrollo |

---

## Estructura del repositorio

```
Assets/
  _Game/
    Scenes/        MainMenu, la intro y las cuatro escenas de nivel
    Scripts/       codigo C# por dominios: Personaje, Niveles, Interfaz, Audio, FX
    Art/           hojas de sprites, capas de nivel, fuentes
    Animation/     controladores y clips del personaje y de los enemigos
    Prefabs/       los enemigos montados
    Editor/        comandos de menu que construyen los niveles y compilan
Tools/             utilidades en Python: preparacion de capas, hojas y prompts
Docs/              documentacion tecnica del proyecto
distrib/           el juego compilado, una carpeta por plataforma
```

### Convenciones que no se rompen

- Ekkar mide **240 px** en pantalla a 1080p. Es la referencia contra la que se
  dimensiona todo lo demás.
- **100 píxeles = 1 unidad** de Unity, en todo el proyecto.
- Cámara ortográfica de tamaño 5,4, bloqueada en `y = 2.6`. Suelo en `y = 0`.
- Paleta cerrada de nueve colores. Ningún elemento introduce uno nuevo.
- Filtro `Point` y sin compresión: el píxel se ve cuadrado.

### Los niveles se construyen por código

No se montan a mano en el editor: los levanta un comando de menú que coloca las
capas, el suelo con colisión, los props, las hogueras y los enemigos. La razón
es que un nivel montado a mano no se puede rehacer sin perder el trabajo, y
estos se han rehecho muchas veces.

```
Ekkar/Niveles/Construir Edad Media
Ekkar/Niveles/Construir Era Industrial
Ekkar/Niveles/Construir Futuro Digital
Ekkar/Niveles/Construir Hora Cero
Ekkar/Compilar/Windows 64 bits
```

---

## Estado

El juego es jugable de principio a fin: cuatro eras, quince enemigos, los cuatro
campeones y el combate final por fases.

Lo que queda pendiente está anotado en
[`Docs/02_Obstaculos_y_Particulas.md`](Docs/02_Obstaculos_y_Particulas.md): los
obstáculos ilustrados de cada era y las láminas de partículas están generados
pero todavía no importados al motor.

---

## Sobre el proyecto

Proyecto formativo del **SENA**, Tecnólogo en Programación de Videojuegos.

El arte de escenarios y enemigos se generó con IA a partir de prompts escritos
para este proyecto —están en `Tools/prompts/`, en texto, para que se pueda ver
cómo se pidió cada pieza— y después se procesó con las utilidades de `Tools/`
para ajustarlo a la escala y la paleta del juego. El código, el diseño de
niveles, las mecánicas y el audio son propios.
