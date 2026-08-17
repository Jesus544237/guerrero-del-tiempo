# -*- coding: utf-8 -*-
"""
Ordena las imagenes que caen en Descargas y las deja donde el juego las espera.

Tu generas los obstaculos y las particulas en la app de Gemini y las bajas sin
mirar el nombre. Esto las clasifica: mira el tamano y la forma de cada imagen,
la compara con lo que pide cada prompt, y propone a que obstaculo corresponde.

No pisa nada sin permiso: por defecto solo ENSENA lo que haria. Con --aplicar
mueve de verdad, y las que no reconoce las deja en una carpeta aparte para
mirarlas a mano.

Uso:
    python Tools/ordena_descargas.py                 # solo dice que haria
    python Tools/ordena_descargas.py --aplicar
    python Tools/ordena_descargas.py --desde "C:/Users/usuario/Downloads"
"""

import argparse
import io
import os
import sys
import time
import re
import shutil

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PROMPTS = os.path.join(HERE, "prompts")
DESCARGAS = os.path.join(os.path.expanduser("~"), "Downloads")
IMG_EXT = (".png", ".jpg", ".jpeg", ".webp")


def catalogo():
    """Lee los prompts y saca que se espera de cada obstaculo: nombre y forma."""
    fichas = []
    for era in sorted(os.listdir(PROMPTS)):
        base = os.path.join(PROMPTS, era, "obstaculos")
        if not os.path.isdir(base):
            continue
        for fn in sorted(os.listdir(base)):
            if not fn.endswith(".txt") or fn.startswith("_"):
                continue
            texto = open(os.path.join(base, fn), encoding="utf-8").read()
            m = re.search(r"reads at (\d+) pixels tall and roughly (\d+) pixels wide", texto)
            if not m:
                continue
            alto, ancho = int(m.group(1)), int(m.group(2))
            fichas.append(dict(era=era, nombre=fn[:-4], tipo="obstaculo",
                               proporcion=ancho / max(1, alto),
                               destino=os.path.join(base, "salida", fn[:-4] + ".png")))
        # particulas: una por era
        pp = os.path.join(PROMPTS, era, "particulas")
        if os.path.isdir(pp):
            fichas.append(dict(era=era, nombre=f"particulas_{era}", tipo="particulas",
                               proporcion=1.6,
                               destino=os.path.join(pp, "salida", "particulas.png")))
    return fichas


def analiza(path):
    """Proporcion de la imagen y cuanto ocupa el objeto dentro del verde."""
    try:
        im = Image.open(path).convert("RGB")
    except Exception:                                   # noqa: BLE001
        return None
    im.thumbnail((320, 320))
    a = np.asarray(im).astype(int)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    fondo = (g - np.maximum(r, b) > 40) & (g > 70)
    obj = ~fondo
    if obj.sum() < 50:
        return dict(proporcion=im.size[0] / max(1, im.size[1]), verde=float(fondo.mean()),
                    piezas=0, prop_obj=im.size[0] / max(1, im.size[1]))
    ys = np.flatnonzero(obj.any(axis=1))
    xs = np.flatnonzero(obj.any(axis=0))
    alto = max(1, ys[-1] - ys[0])
    ancho = max(1, xs[-1] - xs[0])

    # cuantas manchas sueltas hay: muchas = hoja de particulas
    col = obj.any(axis=0).astype(int)
    piezas = int(((col[1:] == 1) & (col[:-1] == 0)).sum())

    return dict(proporcion=im.size[0] / max(1, im.size[1]),
                prop_obj=ancho / alto, verde=float(fondo.mean()), piezas=piezas)


def main():
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
    ap = argparse.ArgumentParser()
    ap.add_argument("--desde", default=DESCARGAS)
    ap.add_argument("--aplicar", action="store_true")
    ap.add_argument("--horas", type=float, default=24,
                    help="solo mira lo descargado en las ultimas N horas")
    a = ap.parse_args()

    fichas = catalogo()
    if not fichas:
        print("No hay prompts de obstaculos. Ejecuta antes make_obstacle_prompts.py")
        return

    limite = time.time() - a.horas * 3600
    archivos = [os.path.join(a.desde, f) for f in sorted(os.listdir(a.desde))
                if f.lower().endswith(IMG_EXT)
                and os.path.getmtime(os.path.join(a.desde, f)) >= limite]
    if not archivos:
        print(f"No hay imagenes en {a.desde}")
        return

    print(f"{len(archivos)} imagenes en Descargas, {len(fichas)} huecos que llenar\n")

    usados = set()
    dudosas = []
    for ruta in archivos:
        info = analiza(ruta)
        if info is None or info["verde"] < 0.25:
            dudosas.append((ruta, "no parece tener fondo verde"))
            continue

        # muchas manchas sueltas -> es una hoja de particulas
        if info["piezas"] >= 8:
            libres = [f for f in fichas if f["tipo"] == "particulas" and f["nombre"] not in usados]
        else:
            libres = [f for f in fichas if f["tipo"] == "obstaculo" and f["nombre"] not in usados]

        if not libres:
            dudosas.append((ruta, "ya no quedan huecos de ese tipo"))
            continue

        # se queda con el que tenga la proporcion mas parecida
        mejor = min(libres, key=lambda f: abs(f["proporcion"] - info["prop_obj"]))
        usados.add(mejor["nombre"])
        dif = abs(mejor["proporcion"] - info["prop_obj"])
        seguro = "seguro " if dif < 0.25 else "dudoso "
        print(f"  {seguro} {os.path.basename(ruta)[:44]:46s} -> {mejor['era']}/{mejor['nombre']}")

        if a.aplicar:
            os.makedirs(os.path.dirname(mejor["destino"]), exist_ok=True)
            Image.open(ruta).convert("RGBA").save(mejor["destino"])

    if dudosas:
        print("\n  sin clasificar:")
        for ruta, motivo in dudosas:
            print(f"    {os.path.basename(ruta)[:44]:46s} ({motivo})")
        if a.aplicar:
            aparte = os.path.join(HERE, "out", "descargas_sin_clasificar")
            os.makedirs(aparte, exist_ok=True)
            for ruta, _ in dudosas:
                shutil.copy2(ruta, os.path.join(aparte, os.path.basename(ruta)))
            print(f"    copiadas a {aparte}")

    faltan = [f["nombre"] for f in fichas if f["nombre"] not in usados]
    if faltan:
        print(f"\n  siguen faltando {len(faltan)}: {', '.join(faltan[:8])}"
              f"{'...' if len(faltan) > 8 else ''}")

    if not a.aplicar:
        print("\n(esto era solo una propuesta; anade --aplicar para moverlas de verdad)")


if __name__ == "__main__":
    main()
