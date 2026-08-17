# Guerrero del Tiempo — estado del proyecto

> Documento de traspaso. Pegar al empezar un chat nuevo para dar contexto.

## 1. Qué es

Juego de plataformas y acción 2D pixel art para PC (1920×1080, 16:9).
Proyecto de Jesús Alexander Zabala Torres — SENA, Programación de Videojuegos.

- **Unity 6.5 (6000.5.5f1)**, URP 2D, Input System nuevo (`activeInputHandler: 1`)
- **Biblioteca de arte original:** vive fuera del repositorio. Los scripts de
  `Tools/` la localizan con la variable de entorno `EKKAR_ARTE`.
- Hay un **MCP de Unity** conectado: se puede compilar, ejecutar menús y leer la escena desde el chat.

## 2. Historia

El **Señor del Tiempo** no destruye el tiempo: lo detiene. Fue el primer guardián
del Gran Reloj y, tras ver a cada era acabar en ruina, lo rompió para congelar
cada época en el instante *anterior* a su desastre. Nadie muere, nadie vive,
todos repiten su peor hora.

Ekkar sobrevivió porque estaba dentro del núcleo del Reloj. Debe recuperar las
tres piezas y repararlo — lo que significa **dejar que cada era termine de morir**.

Las tres piezas siguen la historia real de la relojería:
**el Badajo** (campana, medieval) → **el Volante** (escape mecánico, industrial)
→ **el Cuarzo** (oscilador, digital).

## 3. Escenas

| Escena Unity | Nombre en ficción |
|---|---|
| `MainMenu` | La Torre Quebrada |
| `01_EdadMedia_SitioEterno` | **El Sitio Eterno** |
| `02_EraIndustrial_FundicionDeLasHoras` | **La Fundición de las Horas** |
| `03_FuturoDigital_NeonSinManana` | **Neón Sin Mañana** |
| `04_HoraCero_VacioEntreSegundos` | **La Hora Cero** |

Las cinco están en Build Settings. Detalle narrativo en `Docs/01_Historia_y_Escenas.md`.

## 4. Convenciones que NO se deben romper

- **Escala canónica:** Ekkar mide **240 px** en pantalla a 1080p. Enemigos normales
  180–220, mini-jefes 300–360, el Señor del Tiempo ~media pantalla.
- **PixelsPerUnit = 100** en todo. Cámara ortográfica **size 5.4** (vista de 10.8
  unidades) y bloqueada en **y = 2.6**. Línea pisable del suelo en **y = 0**.
- **Escala de arte de fondo: 0.70** (`ART_SCALE` en el prep). Todas las capas se
  escalan igual para conservar sus proporciones entre sí.
- **Croma verde `#00FF00`** para todo lo que haya que recortar. Los prompts
  prohíben el verde sobre el personaje.
- Paleta: void `#0A0515`, morado `#1A0A2E` / `#2D1B69` / `#7C3AED`,
  cian `#06B6D4` / `#22D3EE`, oro `#F59E0B` / `#FBBF24`, rojo `#DC2626`,
  blanco `#E2E8F0`.

## 5. Lo que ya está hecho

### Menú principal
Completo y con vida propia: fondo con parallax, Ekkar animado, partículas,
título con animación letra a letra, panel de opciones (música, efectos,
resolución, modo de pantalla, vsync, partículas), créditos con scroll,
confirmación de salida, audio y música **generados por código**
(`ProceduralAudio`, sin archivos externos).

### Personaje
**562 fotogramas, 21 animaciones** importadas desde las hojas de TexturePacker
del SENA, reescaladas al 33 %, reancladas por los pies y convertidas en
AnimationClips + `Ekkar.controller`.

- idle, run, dash (35), chronobreak (72), death (61), detener_tiempo (58),
  salto_doble (55), hurt, envainar/desenvainar, ataques, cargas de maná…
- VRAM: 2153 MB → **212 MB**

### Los cuatro niveles
Construidos por código con parallax, suelo con colisión, props, checkpoints,
música por era y partículas propias que **se revelan gradualmente**.

- Medieval, Industrial y Futuro: recorrido lateral de 82 unidades, con relevo
  de telón al final (la ciudad lejana se disuelve y aparece la estructura grande).
- **Hora Cero:** arena de 58 unidades con los **tres telones fijos uno al lado
  del otro** (x = 9.6, 28.9, 48.2), recorribles a pie.

### Sistemas
- **Guardado** (`GameProgress`, PlayerPrefs): checkpoint, escena, nivel alcanzado,
  nivel superado, muertes. Hogueras temporales (`Checkpoint`) repartidas por cada nivel.
- **Muerte y reaparición** (`PlayerRespawn`) → pantalla de resultado.
- **Pantallas de resultado** (`ResultScreen`): derrota, fragmento recuperado y
  victoria, con la tipografía pixel del menú y **degradado distinto por era**.
- **Menú → niveles:** `Jugar()` continúa la partida guardada; existen también
  `NuevaPartida()` y `Continuar()`.
- **`BossPhaseDirector`:** fase 1 en la arena; fase 2 salta al azar entre los
  telones de las tres eras con un chispazo cian, sin repetir era seguida.

## 6. Herramientas (Python, en `Tools/`)

| Script | Para qué |
|---|---|
| `build_ekkar_sheets.py` | Reempaqueta las hojas de TexturePacker de Ekkar |
| `prepare_medieval_layers.py` | Capas del nivel medieval |
| `prepare_level_layers.py` | Capas de industrial / futuro / hora_cero |
| `make_prompts.py` | Prompts de fondos |
| `make_enemy_video_prompts.py` | **86 prompts de vídeo** de enemigos y jefe |
| `gemini_art.py` | Generación por API (bloqueada, ver §8) |

## 7. Menús del editor (Unity)

- `Ekkar/Personaje/Importar animaciones de Ekkar`
- `Ekkar/Niveles/Construir Edad Media`
- `Ekkar/Niveles/Construir Era Industrial` · `Futuro Digital` · `Hora Cero`
- `Ekkar/Niveles/Añadir pantallas de resultado`
- `Ekkar/Niveles/Añadir fondo de relleno` · `Ajustar muros al encuadre` · `Aplicar ajustes de nivel`
- `Ekkar/Debug/Capturar camara` → `Tools/out/captura_escena.png`

## 8. Trampas conocidas

- **La API de Gemini está bloqueada** para esta clave: `403 PERMISSION_DENIED,
  "Your project has been denied access"` en todos los modelos, incluido texto.
  Además la generación de imágenes por API exige facturación (`limit: 0` en free tier).
  → Todo el arte se genera **a mano en la app de Gemini** y se guarda en disco.
- **El puente MCP falla la primera llamada** a un menú recién compilado.
  Ejecutar `Assets/Refresh` en medio y reintentar.
- **No se puede editar la escena en modo Play**: lanza `InvalidOperationException`.
  Comprobar `get_play_mode_status` antes.
- **Build Settings se escribe en diferido**: hace falta `File/Save Project`.
- **La consola de Unity se queda desfasada** por MCP. Verificar leyendo el
  archivo `.unity` en disco cuando importe de verdad.
- Reconstruir un nivel **borra los ajustes hechos a mano** en esa escena. Por eso
  los arreglos posteriores son comandos *aditivos* aparte.

## 9. Pendiente

1. **Enemigos.** Hay 86 prompts en `Tools/prompts/<era>/enemigos/`. Falta
   generarlos y montar el extractor de vídeo → fotogramas → hoja de sprites.
   Los packs de terceros evaluados **no sirven**: 44×52 px y sin animaciones de
   ataque ni daño. Se descartan y se genera el arte propio de cada era.
2. **Combate.** No hay daño, ni vida, ni IA. Nada llama a `PlayerRespawn.Die()`
   ni a `LevelFlow.BossDefeated()` todavía.
3. **Botón CONTINUAR en el menú** (los métodos existen; falta el botón).
4. Puzles de plataformas con el tiempo detenido (rocas y flechas congeladas).
5. Repasar `make_prompts.py`: los prompts de suelo dicen *"fully opaque across
   the whole image"* y por eso Gemini pinta cielo encima del piso. El prep lo
   recorta automáticamente, pero conviene arreglar el prompt.
