# -*- coding: utf-8 -*-
"""
Busca en las hojas de Ekkar el fallo de recorte que tenian envainar y
desenvainar: la celda parte al personaje y deja un trozo suelto.

Como se detecta. En una celda sana el personaje es una sola mancha pegada —
como mucho la espada o un efecto van aparte, y van al lado, no debajo. Cuando
el .tpsheet corta por el cuello, aparece una mancha separada pegada al borde de
abajo (la cabeza que se cayo de la celda de al lado) y el cuerpo se queda con
el corte plano arriba. Eso es lo que se mide:

  - manchas sueltas con peso, cuyo techo queda por debajo del suelo del cuerpo
  - manchas pegadas al borde inferior de la celda que no tocan al cuerpo
  - cuerpos con el borde de arriba sospechosamente recto y ancho

Uso:
    python Tools/audita_hojas_ekkar.py
    python Tools/audita_hojas_ekkar.py --tiras     # ademas deja las tiras en Tools/out
"""

import argparse
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

PROY = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ANIM = os.path.join(PROY, "Assets", "_Game", "Art", "Characters", "Ekkar", "Anim")
OUT = os.path.join(PROY, "Tools", "out", "auditoria_hojas")


def componentes(mascara):
    """Manchas pegadas, en 4 vecinos. Devuelve (etiquetas, cuantas)."""
    h, w = mascara.shape
    etiq = np.zeros((h, w), np.int32)
    padre = [0]

    def raiz(x):
        while padre[x] != x:
            padre[x] = padre[padre[x]]
            x = padre[x]
        return x

    def une(a, b):
        ra, rb = raiz(a), raiz(b)
        if ra != rb:
            padre[max(ra, rb)] = min(ra, rb)

    siguiente = 1
    for y in range(h):
        fila = mascara[y]
        for x in np.nonzero(fila)[0]:
            arriba = etiq[y - 1, x] if y > 0 else 0
            izq = etiq[y, x - 1] if x > 0 else 0
            if arriba and izq:
                etiq[y, x] = min(arriba, izq)
                une(arriba, izq)
            elif arriba or izq:
                etiq[y, x] = arriba or izq
            else:
                etiq[y, x] = siguiente
                padre.append(siguiente)
                siguiente += 1

    if siguiente == 1:
        return etiq, 0
    tabla = np.array([raiz(i) for i in range(siguiente)], np.int32)
    etiq = tabla[etiq]
    vivos = {v: i + 1 for i, v in enumerate(sorted(set(tabla[1:])))}
    salida = np.zeros_like(etiq)
    for v, i in vivos.items():
        salida[etiq == v] = i
    return salida, len(vivos)


def revisa_celda(celda):
    """Devuelve la lista de pegas que tiene esta celda."""
    a = np.asarray(celda)
    if a.shape[2] < 4:
        return []
    tinta = a[:, :, 3] > 32
    total = tinta.sum()
    if total < 40:
        return ["vacia"]

    h, w = tinta.shape
    # se mira a la mitad de resolucion: mas rapido y no cambia el resultado
    chica = tinta[::2, ::2]
    etiq, cuantas = componentes(chica)
    if cuantas == 0:
        return ["vacia"]

    pesos = np.array([(etiq == i).sum() for i in range(1, cuantas + 1)])
    grande = int(np.argmax(pesos)) + 1
    cajas = {}
    for i in range(1, cuantas + 1):
        ys, xs = np.nonzero(etiq == i)
        cajas[i] = (ys.min() * 2, ys.max() * 2, xs.min() * 2, xs.max() * 2)

    pegas = []
    cy0, cy1, cx0, cx1 = cajas[grande]
    peso_total = pesos.sum()

    for i in range(1, cuantas + 1):
        if i == grande:
            continue
        frac = pesos[i - 1] / peso_total
        if frac < 0.015:
            continue                       # motas y chispas: se ignoran
        y0, y1, x0, x1 = cajas[i]
        solapa_x = min(cx1, x1) - max(cx0, x0)
        if y0 > cy1 and solapa_x > 0:
            pegas.append(f"trozo suelto DEBAJO del cuerpo ({frac:.0%})")
        elif y1 >= h - 3 and y0 > cy0 + (cy1 - cy0) * 0.5:
            pegas.append(f"trozo pegado al borde de abajo ({frac:.0%})")
        elif y1 < cy0 and solapa_x > 0:
            pegas.append(f"trozo suelto ENCIMA del cuerpo ({frac:.0%})")

    # el corte por el cuello deja el cuerpo con el techo recto y ancho
    fila_alta = tinta[cy0:cy0 + 2].any(axis=0)
    ancho_techo = fila_alta.sum()
    if ancho_techo > (cx1 - cx0) * 0.55 and (cx1 - cx0) > 20:
        pegas.append(f"techo del cuerpo recto y ancho ({ancho_techo}px)")

    return pegas


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tiras", action="store_true")
    args = ap.parse_args()

    man = json.load(open(os.path.join(ANIM, "ekkar_anim_manifest.json"), encoding="utf-8"))
    os.makedirs(OUT, exist_ok=True)

    sospechosas = []
    print(f"{'animacion':<34}{'fot.':>5}{'celdas con pegas':>18}   detalle")
    print("-" * 100)

    for a in sorted(man["animations"], key=lambda x: x["name"]):
        ruta = os.path.join(ANIM, a["file"])
        if not os.path.exists(ruta):
            print(f"{a['name']:<34}    ?  FALTA LA HOJA {a['file']}")
            continue

        hoja = Image.open(ruta).convert("RGBA")
        cw, ch, cols = a["cellWidth"], a["cellHeight"], a["cols"]
        malas, ejemplos = [], []
        for i in range(a["frames"]):
            col, fila = i % cols, i // cols
            celda = hoja.crop((col * cw, fila * ch, col * cw + cw, fila * ch + ch))
            pegas = [p for p in revisa_celda(celda) if p != "vacia"]
            if pegas:
                malas.append(i)
                if len(ejemplos) < 2:
                    ejemplos.append(f"[{i}] " + "; ".join(pegas))

        marca = "  <<<" if len(malas) >= max(2, a["frames"] * 0.15) else ""
        print(f"{a['name']:<34}{a['frames']:>5}{len(malas):>18}   {' | '.join(ejemplos)[:56]}{marca}")
        if marca:
            sospechosas.append(a["name"])

        if args.tiras:
            tira = Image.new("RGBA", (a["frames"] * (cw + 2), ch), (28, 28, 38, 255))
            d = ImageDraw.Draw(tira)
            for i in range(a["frames"]):
                col, fila = i % cols, i // cols
                celda = hoja.crop((col * cw, fila * ch, col * cw + cw, fila * ch + ch))
                x = i * (cw + 2)
                tira.alpha_composite(celda, (x, 0))
                color = (230, 70, 70) if i in malas else (80, 80, 110)
                d.rectangle([x, 0, x + cw - 1, ch - 1], outline=color)
            tira.convert("RGB").save(os.path.join(OUT, f"{a['name']}.png"))

    print()
    if sospechosas:
        print("Hojas que hay que mirar a ojo:", ", ".join(sospechosas))
    else:
        print("Ninguna hoja tiene el patron de envainar/desenvainar.")
    if args.tiras:
        print(f"Tiras en {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
