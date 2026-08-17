# -*- coding: utf-8 -*-
"""
Saca una hoja de contactos con la hora escrita en cada fotograma, para poder
elegir a ojo el tramo bueno de un clip y anotarlo en Tools/enemy_cuts.json.

Los clips traen la accion repetida 2 o 3 veces, y la deteccion automatica no
siempre acierta cual es la buena. Esto es lo que se mira para decidirlo.

Uso:
    python Tools/timeline_clip.py medieval soldado_hueso 03_ataque
    python Tools/timeline_clip.py medieval soldado_hueso 03_ataque --paso 0.25
"""

import argparse
import io
import os
import subprocess
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "lineas_tiempo")


def duracion(path):
    r = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration",
                        "-of", "csv=p=0", path], capture_output=True, text=True)
    try:
        return float(r.stdout.strip())
    except ValueError:
        return 10.0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("era")
    ap.add_argument("enemigo")
    ap.add_argument("anim", help="nombre del archivo sin extension, p.ej. 03_ataque")
    ap.add_argument("--paso", type=float, default=0.4, help="segundos entre fotogramas")
    ap.add_argument("--cols", type=int, default=5)
    a = ap.parse_args()

    base = os.path.join(HERE, "prompts", a.era, "enemigos", a.enemigo, "videos")
    ruta = None
    for ext in (".mp4", ".mov", ".webm", ".mkv"):
        p = os.path.join(base, a.anim + ext)
        if os.path.exists(p):
            ruta = p
            break
    if ruta is None:
        sys.exit(f"No encuentro {a.anim} en {base}")

    dur = duracion(ruta)
    ims = []
    t = 0.0
    while t < dur:
        r = subprocess.run(["ffmpeg", "-v", "error", "-ss", f"{t:.2f}", "-i", ruta,
                            "-frames:v", "1", "-f", "image2pipe", "-vcodec", "png", "-"],
                           capture_output=True)
        if r.stdout:
            im = Image.open(io.BytesIO(r.stdout)).convert("RGB")
            im.thumbnail((260, 260), Image.LANCZOS)
            d = ImageDraw.Draw(im)
            d.rectangle([0, 0, 44, 14], fill=(0, 0, 0))
            d.text((3, 2), f"{t:.1f}s", fill=(255, 240, 120))
            ims.append(im)
        t += a.paso

    if not ims:
        sys.exit("no salio ningun fotograma")

    cw, ch = ims[0].size
    filas = (len(ims) + a.cols - 1) // a.cols
    hoja = Image.new("RGB", (a.cols * cw, filas * ch), (12, 12, 18))
    for i, im in enumerate(ims):
        hoja.paste(im, ((i % a.cols) * cw, (i // a.cols) * ch))

    os.makedirs(OUT, exist_ok=True)
    destino = os.path.join(OUT, f"{a.enemigo}__{a.anim}.jpg")
    hoja.save(destino, quality=85)
    print(f"{len(ims)} fotogramas cada {a.paso}s  ->  {destino}")


if __name__ == "__main__":
    main()
