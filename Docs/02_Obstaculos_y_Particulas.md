# Obstáculos y partículas: qué falta y cómo montarlo

> Documento de traspaso. Pegar al empezar un chat nuevo sobre este tema.
> Estado a 17 de agosto de 2026.

---

## 1. El problema, en una línea

Las 27 imágenes de obstáculos y partículas están generadas pero **nunca se
importaron a Unity**. Siguen solo en `Tools/prompts/<era>/…/salida/`. En
`Assets/` no hay ni una.

Existe `Tools/make_obstacle_prompts.py`, que genera los textos con los que se
pidieron las imágenes, pero **el script que las mete en el juego no se escribió
nunca**. El encargo original (`Tools/prompts/_BRIEF_ANTIGRAVITY.md`) lo decía
explícitamente: *«De integrar esto en el juego se encarga otra persona»*.

### Lo que sí se ve en las escenas, y no son estos

Los `Prop_00…Prop_NN` que aparecen de fondo son **recortes del arte de fondo**,
no los obstáculos. Los coloca `BuildProps()` en `LevelSceneBuilder.cs` al azar y
los tiñe de oscuro para que hagan de silueta. No tienen colisión ni papel de
juego.

### Las partículas, un caso aparte

`ParticleField.cs` tiene un campo `particleSprite` que está **vacío**, y el
código dice que si no hay sprite pinta un cuadrado blanco:

```csharp
img.sprite = particleSprite;
if (particleSprite == null) img.color = Color.white;
```

Así que las partículas sí corren, pero son cuadritos blancos. Además, el objeto
`ParticulasDeEra` existe en Industrial, Futuro y Hora Cero, **pero no en
Medieval**.

---

## 2. Hechos verificados

- **Los niveles ya están ampliados.** `LevelDef.length = 140f` en
  `Assets/_Game/Editor/LevelSceneBuilder.cs` (línea 56). Hora Cero es la
  excepción: `length = 58f, arena = true` (línea 115). Se amplió de 82 a 140
  precisamente para que cupieran estas piezas.
- **La escala está fijada:** 100 px = 1 unidad de Unity, Ekkar mide 240 px.
  Los tamaños de cada obstáculo ya están pensados con esa regla, así que **no
  hay que escalar a ojo**.
- **Las imágenes vienen sobre croma verde `#00FF00`** y hay que recortarlas.
- Hay variantes del mismo prompt (`barrera_laser` ×3, `aguja_reloj` ×2,
  `particulas` de hora_cero ×3). Dos de las láminas de partículas de Hora Cero
  salieron sobre **fondo blanco** en vez de verde.

---

## 3. El índice: 23 obstáculos con su papel y su medida

Esto es lo que se acordó y está escrito en `make_obstacle_prompts.py`. El papel
no iba al prompt: iba al índice, *«para saber luego qué hace cada pieza cuando
toque montarlas en la escena»*.

### Medieval — El Sitio Eterno

| Pieza | Medida (u) | Papel |
|---|---|---|
| `roca_catapulta` | 1.7 × 1.7 | plataforma |
| `flechas_congeladas` | 2.8 × 1.1 | plataforma |
| `escalera_asedio` | 1.5 × 4.3 | plataforma |
| `muro_roto` | 3.8 × 2.2 | plataforma |
| `rastrillo` | 3.0 × 3.8 | obstáculo |
| `pinchos_asedio` | 2.5 × 1.0 | peligro |
| `brasero` | 1.3 × 1.8 | peligro |

### Era Industrial — La Fundición de las Horas

| Pieza | Medida (u) | Papel |
|---|---|---|
| `engranaje_plataforma` | 3.2 × 3.2 | plataforma |
| `pasarela` | 4.2 × 0.9 | plataforma |
| `piston` | 1.9 × 3.2 | obstáculo |
| `prensa` | 3.0 × 3.8 | obstáculo |
| `valvula_vapor` | 1.8 × 1.4 | peligro |
| `crisol` | 3.8 × 1.4 | peligro |

### Futuro Digital — Neón Sin Mañana

| Pieza | Medida (u) | Papel |
|---|---|---|
| `plataforma_holografica` | 3.2 × 0.7 | plataforma |
| `suelo_glitch` | 2.6 × 0.8 | plataforma |
| `cartel_neon` | 3.8 × 2.4 | plataforma |
| `torre_servidor` | 2.4 × 4.4 | obstáculo |
| `barrera_laser` | 1.4 × 4.2 | peligro |
| `mina_datos` | 1.5 × 1.5 | peligro |

### Hora Cero — El Vacío Entre Segundos

| Pieza | Medida (u) | Papel |
|---|---|---|
| `fragmento_reloj` | 2.8 × 1.3 | plataforma |
| `aguja_reloj` | 5.2 × 0.9 | plataforma |
| `pilar_congelado` | 2.2 × 4.4 | obstáculo |
| `esquirla_tiempo` | 1.4 × 2.2 | peligro |

**Resumen:** 11 plataformas, 5 obstáculos, 7 peligros.

### Qué implica cada papel

| Papel | Colisión | Comportamiento |
|---|---|---|
| **plataforma** | `PlatformEffector2D`, solo por arriba | Se pisa. Se puede saltar desde abajo y atravesarla |
| **obstáculo** | `BoxCollider2D` sólido | Bloquea el paso. Hay que rodearlo o saltarlo |
| **peligro** | `BoxCollider2D` como disparador | Quita vida al tocarlo. Usa el `Damageable` que ya existe |

---

## 4. La decisión tomada: posiciones escritas a mano

Se descartó repartirlas automáticamente por tramos. **Cada pieza va en una
posición concreta, escrita en una tabla**, para que el nivel quede diseñado y no
repartido al azar.

La tabla de posiciones es lo primero que hay que escribir, y conviene apoyarse
en la estructura de cinco tramos que ya está documentada:

| Tramo | Unidades (nivel de 140) | Intención |
|---|---|---|
| 1. Presentación | 0 – 28 | Enseñar sin castigar. Sin enemigos |
| 2. Primer contacto | 28 – 56 | Presentar el enemigo de infantería |
| 3. Complicación | 56 – 84 | Sumar la altura al combate |
| 4. Prueba | 84 – 112 | Combinar todo lo anterior |
| 5. Campeón | 112 – 140 | Relevo de telón y combate de jefe |

Regla de diseño: los peligros no aparecen en el tramo 1, y las plataformas que
exigen salto doble no aparecen antes del tramo 3.

---

## 5. Las tres piezas que hay que construir

### 5.1. Importador de obstáculos (Python)

`Tools/prepare_obstacles.py`, nuevo.

Entrada: `Tools/prompts/<era>/obstaculos/salida/*.{png,jpg,jpeg}`
Salida: `Assets/_Game/Art/Obstaculos/<era>/<nombre>.png`

Qué tiene que hacer:

1. Quitar el croma verde y el reflejo verde del contorno.
2. Recortar al contenido.
3. Reescalar a la medida declarada en el índice (× 100 px por unidad).
4. Escribir el `.meta` con `PixelsPerUnit = 100`, filtro `Point`, sin compresión.

**No hay que escribir el recortador desde cero.** Ya existe uno probado, con
despill del verde incluido, en
`C:\Users\zt200\Documents\SENA\programacion de video juegos\_fuente entregas\preparar_imagenes.py`
(funciones `quitar_fondo` y `recortar_a_contenido`). Detecta el color de fondo
por las esquinas, así que sirve igual para las láminas que salieron sobre
blanco.

### 5.2. Recortador de partículas (Python)

Las cuatro láminas son rejillas de motas sueltas sobre croma. Hay que partirlas
en sprites individuales —el encargo original ya avisaba de que *«un programa las
va a recortar una a una»*— y dejarlas en
`Assets/_Game/Art/Particulas/<era>/`.

Después hay que **asignar una al campo `particleSprite`** de cada
`ParticleField`, y **crear el objeto `ParticulasDeEra` en Medieval**, que no lo
tiene.

### 5.3. Colocador en escena (C#, editor)

`Assets/_Game/Editor/Obstacleplacer.cs`, nuevo, con su entrada de menú
(`Ekkar/Niveles/Colocar obstáculos`).

Lee la tabla de posiciones, instancia cada sprite en su sitio y le pone el
colisionador que le toca según su papel.

**Importante:** tiene que ser un comando **aditivo**, aparte de
`LevelSceneBuilder`. Reconstruir un nivel borra lo que se haya hecho a mano en
esa escena, así que los obstáculos no pueden depender de la reconstrucción.
Es la misma regla que ya siguen `Ekkar/Niveles/Ajustar muros al encuadre` y
`Ekkar/Niveles/Aplicar ajustes de nivel`.

---

## 6. Trampas conocidas

- **El puente MCP falla la primera llamada** a un menú recién compilado.
  Ejecutar `Assets/Refresh` en medio y reintentar. También da *timeout* aunque la
  operación sí se haya ejecutado: comprobar leyendo el `.unity` en disco.
- **No se puede editar la escena en modo Play.** Comprobar
  `get_play_mode_status` antes.
- **Reconstruir un nivel borra los ajustes hechos a mano.** De ahí que el
  colocador tenga que ser aditivo.
- **Las variantes repetidas** (`barrera_laser` ×3, `aguja_reloj` ×2) hay que
  elegirlas, no importarlas todas. Criterio ya acordado: que respeten la paleta
  de su era.
