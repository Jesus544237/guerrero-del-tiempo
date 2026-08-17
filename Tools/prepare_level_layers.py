# -*- coding: utf-8 -*-
"""
Prepara las capas de un nivel cualquiera (industrial, futuro, hora_cero).

Hace lo mismo que prepare_medieval_layers.py pero para todas las eras, y
ademas recorta el cielo pintado que traen las capas de suelo: el prompt pedia
"opaco en toda la imagen", asi que Gemini pinto cielo por encima del piso y
esa banda taparia las capas de fondo. Se detecta el borde superior del suelo
por varianza horizontal (el cielo es un degradado liso, el suelo tiene
detalle) y todo lo que queda arriba pasa a transparente.

Uso:
    python Tools/prepare_level_layers.py            (todas las eras)
    python Tools/prepare_level_layers.py futuro
"""

import glob
import json
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(HERE)
ART = os.path.join(PROJ, "Assets", "_Game", "Art", "Backgrounds")

ART_SCALE = 0.70
MIN_PROP_AREA = 2500

# era -> (carpeta destino, prefijos de capa en orden: cielo, medio-lejos, medio, suelo, props)
ERAS = {
    "industrial": ("Industrial", ["00_cielo", "01_horizonte", "02_fabrica", "03_suelo", "04_frente"]),
    "futuro":     ("Futuro",     ["00_cielo", "01_horizonte", "02_servidor", "03_suelo", "04_frente"]),
    "hora_cero":  ("HoraCero",   ["00_vacio", "01_ecos", "01(2)_ecos", "02_trono", "03_suelo", "04_frente"]),
}


def source_dir(era):
    """El nombre de la carpeta recortada varia entre eras."""
    base = os.path.join(HERE, "prompts", era, "scenes")
    for name in ("sin_fondo", "scenes_sin_fondo"):
        d = os.path.join(base, name)
        if os.path.isdir(d):
            return d
    raise SystemExit(f"No encuentro la carpeta recortada de '{era}' bajo {base}")


def find_source(d, prefix):
    hits = sorted(glob.glob(os.path.join(d, glob.escape(prefix) + "*.png")))
    return hits[0] if hits else None


def load_scaled(path):
    im = Image.open(path).convert("RGBA")
    w = max(1, int(round(im.size[0] * ART_SCALE)))
    h = max(1, int(round(im.size[1] * ART_SCALE)))
    return im.resize((w, h), Image.LANCZOS)


def floor_top_row(im):
    """
    Primera fila donde empieza el suelo de verdad.

    El cielo pintado es un degradado liso: dentro de cada fila apenas hay
    variacion. El suelo tiene rejillas, tubos y grietas, asi que su desviacion
    horizontal se dispara. Se busca el primer tramo sostenido de filas con
    mucho detalle.
    """
    g = np.array(im.convert("L")).astype(np.float32)
    h = g.shape[0]

    # El borde del piso es una linea horizontal dura que cruza TODO el ancho,
    # asi que aparece como un salto de luminancia entre filas consecutivas.
    # (La varianza dentro de la fila no sirve: los rayos y engranajes que
    # Gemini pinta en el cielo tambien la disparan.)
    diff = np.abs(np.diff(g, axis=0)).mean(axis=1)

    lo = int(h * 0.15)
    hi = int(h * 0.75)
    band = diff[lo:hi]
    if band.size == 0:
        return h // 2
    return lo + int(np.argmax(band)) + 1


def cut_above(im, row):
    """Deja transparente todo lo que hay por encima de la fila indicada."""
    arr = np.array(im)
    arr[:row, :, 3] = 0
    return Image.fromarray(arr, "RGBA")


def trim_dark_fringe(im, threshold=78):
    arr = np.array(im)
    alpha = arr[..., 3]
    lum = arr[..., :3].astype(np.int32).sum(axis=2)
    opaque = alpha > 200
    dark = opaque & (lum < threshold)
    reach = ~opaque
    while True:
        grown = reach.copy()
        grown[1:, :] |= reach[:-1, :]
        grown[:-1, :] |= reach[1:, :]
        grown[:, 1:] |= reach[:, :-1]
        grown[:, :-1] |= reach[:, 1:]
        new = grown & dark & ~reach
        if not new.any():
            break
        reach |= new
    removed = reach & opaque
    if removed.any():
        alpha[removed] = 0
        arr[..., 3] = alpha
        return Image.fromarray(arr, "RGBA"), 100.0 * removed.mean()
    return im, 0.0


def label_components(mask):
    h, w = mask.shape
    labels = np.zeros((h, w), dtype=np.int32)
    cur = 0
    for sy in range(h):
        for sx in range(w):
            if not mask[sy, sx] or labels[sy, sx]:
                continue
            cur += 1
            stack = [(sy, sx)]
            labels[sy, sx] = cur
            while stack:
                y, x = stack.pop()
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = y + dy, x + dx
                        if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not labels[ny, nx]:
                            labels[ny, nx] = cur
                            stack.append((ny, nx))
    return labels, cur


def split_props(im, out_dir, step=4):
    w, h = im.size
    opaque = np.array(im.getchannel("A")) > 40
    os.makedirs(out_dir, exist_ok=True)
    for old in os.listdir(out_dir):
        if old.endswith(".png"):
            os.remove(os.path.join(out_dir, old))

    labels, count = label_components(opaque[::step, ::step])
    found = []
    for lab in range(1, count + 1):
        ys, xs = np.where(labels == lab)
        if len(ys) * step * step < MIN_PROP_AREA:
            continue
        found.append((int(xs.min()), lab))
    found.sort()

    names = []
    for i, (_x, lab) in enumerate(found):
        comp = Image.fromarray(np.where(labels == lab, 255, 0).astype(np.uint8), "L")
        comp = comp.resize((w, h), Image.NEAREST).filter(ImageFilter.MaxFilter(step + 1))
        iso = im.copy()
        merged = np.minimum(np.array(iso.getchannel("A")), np.array(comp))
        iso.putalpha(Image.fromarray(merged, "L"))
        box = iso.getbbox()
        if box is None:
            continue
        crop = iso.crop(box)
        name = f"prop_{i:02d}.png"
        crop.save(os.path.join(out_dir, name), optimize=True)
        names.append({"file": name, "width": crop.size[0], "height": crop.size[1]})
    return names


def process(era):
    src = source_dir(era)
    folder, prefixes = ERAS[era]
    dst = os.path.join(ART, folder)
    props_dir = os.path.join(dst, "Props")
    os.makedirs(dst, exist_ok=True)

    print(f"\n== {era}  ({src})")
    manifest = {"artScale": ART_SCALE, "layers": [], "props": []}

    for prefix in prefixes:
        path = find_source(src, prefix)
        if path is None:
            print(f"   ! falta {prefix}")
            continue

        im = load_scaled(path)
        key = prefix.replace("(", "_").replace(")", "")
        is_floor = "suelo" in prefix
        is_props = "frente" in prefix

        note = ""
        if is_floor:
            row = floor_top_row(im)
            im = cut_above(im, row)
            note = f"  cielo pintado recortado por encima de y={row}"
        elif not is_props:
            im, removed = trim_dark_fringe(im)
            if removed > 0.5:
                note = f"  restos limpiados {removed:.1f}%"

        if is_props:
            props = split_props(im, props_dir)
            manifest["props"] = props
            print(f"   {prefix:14s} -> {len(props)} props")
            continue

        out_name = f"bg_{key}.png"
        im.save(os.path.join(dst, out_name), optimize=True)

        bb = im.getbbox()
        entry = {"key": key, "file": out_name,
                 "width": im.size[0], "height": im.size[1],
                 "contentTop": int(bb[1]) if bb else 0,
                 "contentBottom": int(bb[3]) if bb else im.size[1]}
        if is_floor:
            a = np.array(im.getchannel("A"))
            rows = (a > 200).mean(axis=1)
            solid = [y for y in range(a.shape[0]) if rows[y] > 0.5]
            entry["groundTopPixel"] = solid[0] if solid else 0
            entry["groundSolidBottom"] = solid[-1] if solid else a.shape[0]
            manifest["groundTopPixel"] = entry["groundTopPixel"]
            manifest["groundHeight"] = im.size[1]
            manifest["groundSolidBottom"] = entry["groundSolidBottom"]

        manifest["layers"].append(entry)
        print(f"   {prefix:14s} {im.size[0]}x{im.size[1]}{note}")

    with open(os.path.join(dst, "level_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
    print(f"   -> {dst}")


def main():
    eras = sys.argv[1:] or list(ERAS)
    for era in eras:
        if era not in ERAS:
            print(f"era desconocida: {era}")
            continue
        process(era)


if __name__ == "__main__":
    main()
