# Encargo para Antigravity: obstáculos y partículas de Guerrero del Tiempo

Copia este texto entero en Antigravity. Está escrito para que se pueda ejecutar
sin más contexto que el propio proyecto.

---

Trabajas en el proyecto Unity `ekkar_juego_del_tiempo`, un juego de plataformas
2D en pixel art. Tu tarea es **generar imágenes**, no tocar código.

## Lo que tienes que hacer

En `Tools/prompts/` hay cuatro carpetas de era: `medieval`, `industrial`,
`futuro`, `hora_cero`. Dentro de cada una:

- `obstaculos/` — un archivo `.txt` por objeto. Son 23 en total.
- `particulas/particulas.txt` — uno por era. Son 4 en total.

Para cada uno de esos 27 archivos:

1. Lee el `.txt` completo.
2. Genera una imagen usando **ese texto tal cual como prompt**, sin resumirlo,
   sin reescribirlo y sin añadirle nada.
3. Guarda el resultado en PNG, dentro de la subcarpeta `salida/` que está al
   lado del prompt, **con el mismo nombre que el `.txt`**.

Ejemplo:

```
lee     Tools/prompts/medieval/obstaculos/roca_catapulta.txt
guarda  Tools/prompts/medieval/obstaculos/salida/roca_catapulta.png
```

## Cómo saber si una imagen vale

Recházala y vuelve a generarla si pasa cualquiera de estas cosas:

- El fondo **no** es verde `#00FF00` plano y uniforme, o tiene degradado,
  viñeta, suelo o sombra proyectada. El verde se recorta después por programa;
  una sombra sobre el fondo se recorta como si fuera parte del objeto.
- Hay **más de un objeto**, o aparece un personaje, una mano, o el mismo objeto
  repetido.
- Hay texto, letras, números, logos o marcas de agua.
- No es pixel art: si sale un render 3D, una ilustración suave o algo con
  desenfoque, no sirve.
- En las **partículas**: si las motas se tocan, se solapan o están unidas por
  un rastro. Tienen que estar sueltas y bien separadas, porque un programa las
  va a recortar una a una. Esto es lo que más falla, revísalo con cuidado.

## Lo que NO debes hacer

- No modifiques ningún `.txt` de prompt.
- No inventes obstáculos nuevos ni te saltes ninguno.
- No toques nada dentro de `Assets/`, ni las escenas `.unity`, ni los scripts
  de C#, ni los de Python. De integrar esto en el juego se encarga otra
  persona.
- No cambies el nombre de los archivos.

## Si no puedes generar imágenes

Dilo claramente y para. En ese caso el plan B es que el usuario pegue los
prompts a mano en la app de Gemini, así que no intentes sustituirlos por
placeholders, ni por assets descargados, ni por imágenes hechas con código.

## Cuando termines

Escribe un resumen con:
- cuántas imágenes has generado y cuáles,
- cuáles has tenido que repetir y por qué,
- cuáles no han salido bien del todo, para revisarlas a mano.
