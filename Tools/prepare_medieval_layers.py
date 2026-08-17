# -*- coding: utf-8 -*-
"""
Prepara las capas del nivel medieval "El Sitio Eterno" para Unity.

Parte de las imagenes ya recortadas a mano en
    Tools/prompts/medieval/scenes/sin_fondo
asi que aqui no se toca el alfa: solo se normaliza el tamano, se separan los
props del primer plano y se mide la linea pisable del suelo.

Uso:
    python Tools/prepare_medieval_layers.py
"""

import glob
import json
import os

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(HERE)
SRC = os.path.join(HERE, "prompts", "medieval", "scenes", "sin_fondo")
DST = os.path.join(PROJ, "Assets", "_Game", "Art", "Backgrounds", "Medieval")
PROPS = os.path.join(DST, "Props")

# Un unico factor para TODAS las capas. Asi se conservan las proporciones
# reales entre ellas: el horizonte queda como una franja baja de ciudad
# lejana y no como un telon a pantalla completa.
ART_SCALE = 0.70
MIN_PROP_AREA = 2500


def find_source(prefix):
    """Los nombres varian un poco (02_castillo_sin__fondo), se busca por prefijo."""
    hits = sorted(glob.glob(os.path.join(SRC, prefix + "*.png")))
    if not hits:
        raise SystemExit(f"No encuentro ninguna imagen que empiece por '{prefix}' en {SRC}")
    return hits[0]


def load_scaled(prefix):
    """Carga la capa y le aplica el factor comun, sin tocar sus proporciones."""
    path = find_source(prefix)
    im = Image.open(path).convert("RGBA")
    original = im.size

    w = max(1, int(round(im.size[0] * ART_SCALE)))
    h = max(1, int(round(im.size[1] * ART_SCALE)))
    im = im.resize((w, h), Image.LANCZOS)

    print(f"  {os.path.basename(path):32s} {original[0]}x{original[1]} -> {w}x{h}")
    return im


def trim_dark_fringe(im, name, threshold=78):
    """
    Limpia los restos de fondo que quedaron opacos tras el recorte manual.

    Solo borra pixeles oscuros que se puedan alcanzar caminando desde una zona
    ya transparente, asi que las juntas oscuras del interior del castillo (que
    estan rodeadas de piedra iluminada) no se tocan. Sin esto quedan bandas
    rectangulares con borde duro flotando sobre el cielo.
    """
    arr = np.array(im)
    alpha = arr[..., 3]
    lum = arr[..., :3].astype(np.int32).sum(axis=2)

    opaque = alpha > 200
    dark = opaque & (lum < threshold)
    reach = ~opaque                      # semillas: lo ya transparente

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
        im = Image.fromarray(arr, "RGBA")
        print(f"  {name}: limpiados {100.0 * removed.mean():.1f}% de restos de fondo")
    return im


def walkable_line(im):
    """Primera fila totalmente solida: el borde de hierba sobre el que se anda."""
    a = np.array(im.getchannel("A"))
    rows = (a > 200).mean(axis=1)
    for y in range(a.shape[0]):
        if rows[y] > 0.5:
            return y
    return a.shape[0] // 2


def solid_bottom(im):
    a = np.array(im.getchannel("A"))
    rows = (a > 200).mean(axis=1)
    ys = [y for y in range(a.shape[0]) if rows[y] > 0.5]
    return max(ys) if ys else a.shape[0]


def label_components(mask):
    """Componentes conexas (vecindad 8) con pila explicita."""
    h, w = mask.shape
    labels = np.zeros((h, w), dtype=np.int32)
    current = 0
    for sy in range(h):
        for sx in range(w):
            if not mask[sy, sx] or labels[sy, sx]:
                continue
            current += 1
            stack = [(sy, sx)]
            labels[sy, sx] = current
            while stack:
                y, x = stack.pop()
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = y + dy, x + dx
                        if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not labels[ny, nx]:
                            labels[ny, nx] = current
                            stack.append((ny, nx))
    return labels, current


def split_props(im, step=4):
    """Separa cada objeto de la hoja del primer plano en su propio PNG."""
    w, h = im.size
    alpha = np.array(im.getchannel("A"))
    opaque = alpha > 40

    os.makedirs(PROPS, exist_ok=True)
    for old in os.listdir(PROPS):
        if old.endswith(".png"):
            os.remove(os.path.join(PROPS, old))

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

        isolated = im.copy()
        merged = np.minimum(np.array(isolated.getchannel("A")), np.array(comp))
        isolated.putalpha(Image.fromarray(merged, "L"))

        box = isolated.getbbox()
        if box is None:
            continue
        crop = isolated.crop(box)

        name = f"prop_{i:02d}.png"
        crop.save(os.path.join(PROPS, name), optimize=True)
        names.append({"file": name, "width": crop.size[0], "height": crop.size[1]})
        print(f"  prop_{i:02d}  {crop.size[0]}x{crop.size[1]}")

    return names


def main():
    os.makedirs(DST, exist_ok=True)
    os.makedirs(PROPS, exist_ok=True)

    print("Capas (ya vienen recortadas a mano):")
    cielo = load_scaled("00_cielo")
    horizonte = load_scaled("01_horizonte")
    castillo = load_scaled("02_castillo")
    suelo = load_scaled("03_suelo")
    frente = load_scaled("04_frente")

    print("\nLimpieza de restos:")
    horizonte = trim_dark_fringe(horizonte, "horizonte")
    castillo = trim_dark_fringe(castillo, "castillo")
    # La hoja de props NO se limpia: sus objetos son casi siluetas negras, y el
    # barrido se los comeria por dentro, partiendo cada prop en trozos sueltos.

    cielo.save(os.path.join(DST, "bg_cielo.png"), optimize=True)
    horizonte.save(os.path.join(DST, "bg_horizonte.png"), optimize=True)
    castillo.save(os.path.join(DST, "bg_castillo.png"), optimize=True)
    suelo.save(os.path.join(DST, "bg_suelo.png"), optimize=True)

    walk = walkable_line(suelo)
    bottom = solid_bottom(suelo)
    print(f"\n  linea pisable del suelo: y={walk} de {suelo.size[1]}")
    print(f"  suelo solido hasta y={bottom}  ->  {(bottom - walk) / 100:.2f} unidades bajo los pies")
    if (bottom - walk) / 100 < 5.0:
        print("  AVISO: puede verse el vacio por debajo del suelo en pantalla.")

    print("\nProps del primer plano:")
    props = split_props(frente)

    def metrics(key, im):
        bb = im.getbbox()
        return {
            "key": key,
            "width": im.size[0],
            "height": im.size[1],
            "contentTop": int(bb[1]) if bb else 0,
            "contentBottom": int(bb[3]) if bb else im.size[1],
        }

    manifest = {
        "artScale": ART_SCALE,
        "groundTopPixel": int(walk),
        "groundHeight": suelo.size[1],
        "groundSolidBottom": int(bottom),
        "layers": [
            metrics("cielo", cielo),
            metrics("horizonte", horizonte),
            metrics("castillo", castillo),
            metrics("suelo", suelo),
        ],
        "props": props,
    }
    with open(os.path.join(DST, "medieval_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)

    print(f"\nListo -> {DST}")


if __name__ == "__main__":
    main()
