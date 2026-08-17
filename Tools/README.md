# Tools — generacion de arte con Gemini

## 1. Conseguir la clave

1. Entra en <https://aistudio.google.com/apikey> con tu cuenta de Google.
2. "Create API key". Copiala.
3. Guardala en `Tools/gemini_key.txt` (una sola linea, sin comillas ni espacios).

> **Importante:** la suscripcion *Gemini Pro* de la app es una cosa y la *API de
> Google AI Studio* es otra. La clave se saca aparte y tiene su propia cuota.
> La generacion de imagenes puede pedirte activar facturacion en el proyecto de
> Google Cloud asociado; en la propia pagina de la clave te lo indica.

> **Nunca** pegues la clave en un chat ni la subas a GitHub. El archivo
> `gemini_key.txt` esta pensado para quedarse solo en tu equipo.

## 2. Comprobar que funciona

```bash
python Tools/gemini_art.py --list
```

Te lista los modelos que tu clave puede usar y marca los que generan imagenes.
Si la lista de imagen sale vacia, la clave no tiene la generacion habilitada.

## 3. Generar

Fondo (sin referencias, usa Imagen y respeta el 16:9):

```bash
python Tools/gemini_art.py --prompt-file Tools/prompts/medieval/02_castillo.txt --out Tools/out/02_castillo.png --aspect 16:9
```

Personaje (con referencia, para que respete el estilo de Ekkar) y recorte del
fondo magenta a transparencia:

```bash
python Tools/gemini_art.py --prompt-file Tools/prompts/enemigos/01_esqueleto.txt --ref Tools/refs/ekkar.png --out Tools/out/esqueleto.png --chroma
```

Cuatro variantes para elegir:

```bash
python Tools/gemini_art.py --prompt-file Tools/prompts/medieval/00_cielo.txt --out Tools/out/cielo.png --count 4
```

## 4. Si prefieres no usar la API

Los archivos de `Tools/prompts/` son texto plano pensado para copiarse y
pegarse tal cual en la app de Gemini. Adjunta ahi mismo `Tools/refs/ekkar.png`
cuando el prompt hable de "the attached reference image", descarga el
resultado y dejalo en `Tools/out/`. El pipeline de Unity funciona igual.

## 5. Enemigos: de los clips de video a la hoja de sprites

Los enemigos no se generan como imagen fija sino como **clip de video** (un
prompt por animacion, en `prompts/<era>/enemigos/<enemigo>/`). El clip se deja
en la subcarpeta `videos/` de ese enemigo con el nombre del prompt
(`03_ataque.mp4`) y de ahi salen las hojas:

```bash
python Tools/build_enemy_sheets.py --listar     # que clips ve
python Tools/build_enemy_sheets.py              # monta todo lo que haya
python Tools/build_enemy_sheets.py --enemigo soldado_hueso
```

Hace lo mismo que `build_ekkar_sheets.py`, pero partiendo del video: saca los
fotogramas con **ffmpeg**, recorta el croma verde con borde suave y le quita el
derrame, ignora la esquina inferior derecha (la que los prompts dejan vacia
porque ahi cae la marca de Gemini), elige el trozo util del clip, reancla y
escala al tamano canonico del enemigo.

- Hojas y manifiesto → `Assets/_Game/Art/Characters/Enemigos/<era>/<enemigo>/`
- Revision visual → `Tools/out/preview_enemigos/<enemigo>/` (cebolla y tira)

El **trozo util** se busca solo: en `idle` y `caminar` detecta el periodo del
ciclo y se queda con **un ciclo**; en `ataque`, `dano` y `muerte` mide cuanto se
aleja cada fotograma de la pose neutra y se queda con **una repeticion**. Si un
clip sale mal cortado, se le marca a mano:

```bash
python Tools/build_enemy_sheets.py --enemigo soldado_hueso --anim 03_ataque --desde 3.5 --hasta 4.6 --fotogramas 12
```

La **altura** de cada enemigo esta en la tabla `ROSTER` del script, sacada de su
propio prompt ("two thirds the hero's height"...), con Ekkar a 240 px como
referencia. Se mide en la animacion de referencia (`idle`) y el mismo factor se
aplica a todas sus animaciones, para que no cambie de talla entre una y otra.
Se pisa con `--altura`.

Los que **flotan** (espectro, drones, fragmento, el Senor del Tiempo) se anclan
a una linea comun en vez de por los pies: asi el cabeceo se conserva en lugar de
quedar aplanado. Se fuerza con `--ancla pies|vuelo`.

## Carpetas

- `refs/` — imagenes canonicas. `ekkar.png` y `senor_del_tiempo.png` son la
  referencia obligatoria de diseno: si algo generado no se parece a estas,
  se descarta y se vuelve a generar.
- `prompts/` — un archivo por imagen. `_ESTILO.txt` es el bloque comun.
- `out/` — resultados en crudo, antes de entrar en `Assets/`.
