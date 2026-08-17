# -*- coding: utf-8 -*-
"""
Reempaqueta las hojas de sprites de Ekkar hechas con TexturePacker.

Que hace, y por que:
  - Lee cada .tpsheet y recorta sus fotogramas del PNG empaquetado.
  - Une las partes (parte_1, parte_2, ...) en una sola animacion.
  - Reancla cada fotograma por el centro-abajo de su caja alfa, para que el
    personaje no salte entre fotogramas ni entre animaciones.
  - Reescala al 33 % (Ekkar queda a ~240 px de alto, la escala canonica).
  - Escribe una rejilla uniforme por animacion + un manifiesto JSON que
    consume el importador de Unity.
  - Genera una "cebolla" (todos los fotogramas superpuestos) por animacion
    para poder revisar de un vistazo si el anclaje quedo bien.

Uso:
    python Tools/build_ekkar_sheets.py                    # todas
    python Tools/build_ekkar_sheets.py envainar desenvainar   # solo esas, y
                                                          # el resto se respeta
"""

import json
import math
import os
import re
import sys

from PIL import Image

# La biblioteca de arte original vive fuera del repositorio. Se localiza con la
# variable de entorno EKKAR_ARTE; si no esta definida, se busca en la ruta
# habitual dentro de la carpeta del usuario que ejecuta el script.
SRC_ROOT = os.environ.get("EKKAR_ARTE") or os.path.join(
    os.path.expanduser("~"), "Documents", "SENA", "programacion de video juegos", "ekkar")
PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(PROJ, "Assets", "_Game", "Art", "Characters", "Ekkar", "Anim")
PREVIEW_DIR = os.path.join(PROJ, "Tools", "out", "preview_anim")

SCALE = 0.33
BOTTOM_MARGIN = 4      # px libres bajo los pies, dentro de la celda
SIDE_MARGIN = 6
MAX_COLS = 8

# tope de fotogramas por animacion: los saltos venian con 22 y 55, que en
# pantalla duran una eternidad. Se muestrean parejos hasta el tope.
MAX_FRAMES = {"salto_normal": 12, "salto_doble": 16, "death": 28, "chronobreak": 34,
              "detener_tiempo": 26, "salto_autoataque_vertical": 14}
DEFAULT_FPS = 12

# fps por animacion (lo que no aparezca usa DEFAULT_FPS)
FPS = {
    # quietas y de lectura: lentas, para que se aprecie el dibujo
    "idle": 9,
    "envainar": 12,
    "desenvainar": 12,
    # desplazamiento
    "run": 14,
    "salto_normal": 14,
    "salto_doble": 16,
    "dash": 22,                       # el dash tiene que sentirse instantaneo
    # Golpes. El fps sale de cuanto tiene que durar el clip, no al reves:
    # el autoataque se puede repetir cada 0,42 s (PlayerCombat.cadencia), asi
    # que un clip de 0,94 s iba siempre por detras de la tecla y se sentia
    # pastoso. Ahora el dibujo cabe dentro de la cadencia.
    "ataque_horizontal": 33,          # 15 fot -> 0,45 s
    "ataque_vertical": 22,            # 10 fot -> 0,45 s
    "salto_autoataque_vertical": 26,  # 14 fot -> 0,54 s
    # el cargado pesa mas, pero tampoco puede arrastrarse: 0,70 s los cinco
    "autoataque_carga_mana_forma_1": 23,   # 16 fot
    "autoataque_carga_mana_forma_2": 23,   # 16 fot
    "autoataque_carga_mana_forma_3": 33,   # 23 fot
    "autoataque_carga_mana_forma_4": 29,   # 20 fot
    "autoataque_carga_mana_forma_5": 17,   # 12 fot
    "salto_horizontal_carga_mana": 33,     # 23 fot
    "salto_vertical_carga_mana": 24,       # 17 fot
    # reacciones y habilidades: se dejan respirar
    "hurt": 14,
    "death": 11,
    # el chronobreak es el conjuro largo: Ekkar embiste, clava la espada y
    # entonces revienta. A 18 fps se veia como un manotazo
    "chronobreak": 12,                # 34 fot -> 2,83 s
    "detener_tiempo": 13,             # 26 fot -> 2,00 s
}

# nombres finales mas comodos
RENAME = {
    "runing": "run",
    "salto_vertical": "salto_autoataque_vertical",
}

# Los fotogramas NO salen del .tpsheet, salen de los PNG sueltos numerados que
# hay en esa misma carpeta.
#
# Se descubrio con envainar y desenvainar: sus rectangulos partian al personaje
# por el cuello, y cada celda salia con el cuerpo sin cabeza y la cabeza pegada
# al pie de la celda de al lado. Al auditarlo, el problema estaba en todas: no
# hay una sola animacion donde el numero de rectangulos del .tpsheet coincida
# con el de fotogramas de verdad — declaran de mas porque parten fotogramas en
# trozos. En death se veia clarisimo: celdas vacias, poses sueltas y el orden
# revuelto, con 61 fotogramas sanos al lado sin usar.
#
# Los .tpsheet siguen sirviendo para una cosa: dicen que animacion y que parte
# es cada carpeta. De ahi que collect() los siga buscando.

# Animaciones que se saltan stabilize() (ver mas abajo).
#
# stabilize compara la X del pie entre fotogramas, pero esa X va medida dentro
# del recorte de cada uno. Al desenvainar, la hoja sale por la izquierda y
# ensancha el recorte 45 px, con lo que el pie -que no se ha movido- aparece
# mucho mas a la derecha y stabilize lo toma por una deteccion contaminada.
# Corregirlo empujaba el cuerpo un cuarto de unidad de lado en los cuatro
# fotogramas del desenvaine. Aqui la deteccion es de fiar (no hay efectos a
# ras de suelo), asi que se deja pasar tal cual.
SIN_ESTABILIZAR = {"envainar", "desenvainar"}


def anim_key(tpsheet_name):
    """De 'dash_parte_1_sin_fondo_packer.tpsheet' saca ('dash', 1)."""
    base = tpsheet_name
    for suffix in ("_packer.tpsheet", ".tpsheet"):
        if base.endswith(suffix):
            base = base[: -len(suffix)]
            break
    base = base.replace("_sin_fondo", "")

    part = 1
    m = re.search(r"parte[_-](\d+)", base)
    if m:
        part = int(m.group(1))
        base = re.sub(r"_?parte[_-]\d+_?", "_", base)

    base = re.sub(r"_+", "_", base).strip("_").lower()
    base = RENAME.get(base, base)
    return base, part


def parse_tpsheet(path):
    """Devuelve (texture_name, sheet_size, [(name, x, y, w, h), ...])."""
    texture = None
    size = None
    frames = []
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            if line.startswith(":"):
                if line.startswith(":texture="):
                    texture = line.split("=", 1)[1].strip()
                elif line.startswith(":size="):
                    w, h = line.split("=", 1)[1].strip().split("x")
                    size = (int(w), int(h))
                continue
            parts = [s.strip() for s in line.split(";")]
            if len(parts) < 5:
                continue
            try:
                frames.append((parts[0], int(parts[1]), int(parts[2]), int(parts[3]), int(parts[4])))
            except ValueError:
                continue
    return texture, size, frames


def frame_sort_key(name):
    m = re.match(r"^(\d+)", name)
    return (0, int(m.group(1))) if m else (1, name)


def collect():
    """
    Agrupa las carpetas de fotogramas por animacion y parte.

    El .tpsheet no se lee aqui: solo se usa su nombre y donde esta, que es lo
    que dice a que animacion pertenece la carpeta y en que orden van las partes.
    """
    groups = {}
    for dirpath, _dirs, files in os.walk(SRC_ROOT):
        low = dirpath.lower()
        if any(skip in low for skip in ("gothicvania", "cyberpunk", "industrial", "drones")):
            continue
        for fn in files:
            if not fn.endswith(".tpsheet"):
                continue
            key, part = anim_key(fn)
            groups.setdefault(key, []).append((part, os.path.join(dirpath, fn)))
    for key in groups:
        groups[key].sort()
    return groups


def frames_de_carpeta(folder):
    """
    Los PNG sueltos '<n>_loquesea.png' de una carpeta, en orden numerico.

    Es la fuente buena. El .tpsheet de al lado solo sirve ya para saber que
    animacion y que parte es cada carpeta.
    """
    if not os.path.isdir(folder):
        return []

    numerados = []
    for fn in os.listdir(folder):
        m = re.match(r"^(\d+)_.*\.png$", fn, re.IGNORECASE)
        if m:
            numerados.append((int(m.group(1)), fn))
    numerados.sort()

    frames = []
    for _n, fn in numerados:
        im = Image.open(os.path.join(folder, fn)).convert("RGBA")
        # TexturePacker los entregaba recortados a su caja alfa; se recorta
        # igual para que el anclaje se comporte como siempre
        box = im.getbbox()
        frames.append(im.crop(box) if box else im)
    return frames


def alpha_anchor(img):
    """
    Ancla = punto de apoyo del personaje.

    El centro de la caja alfa no sirve: en los ataques la estela de la espada
    ensancha la caja y arrastra el ancla, con lo que el cuerpo se desplaza de
    un fotograma a otro. Se usa en su lugar el centro horizontal de los pies:
    la banda inferior de pixeles opacos, que es siempre el calzado.
    """
    bbox = img.getbbox()
    if bbox is None:
        return None
    x0, y0, x1, y1 = bbox
    h = y1 - y0

    band = max(6, int(h * 0.12))
    rgba = img.load()

    # Los efectos del juego son cian/azul; la armadura y las botas de Ekkar son
    # plateadas y su capa roja. Descartando lo azul dominante queda solo el
    # personaje, incluso en el definitivo, donde la energia cubre todo el suelo.
    body, any_solid = [], []
    for y in range(max(y0, y1 - band), y1):
        for x in range(x0, x1):
            r, g, b, a = rgba[x, y]
            if a <= 180:
                continue
            any_solid.append(x)
            if b <= r + 40:
                body.append(x)

    if len(body) >= 20:
        foot_x = sum(body) // len(body)
    elif len(any_solid) >= 20:
        foot_x = sum(any_solid) // len(any_solid)
    else:
        foot_x = (x0 + x1) // 2
    return (foot_x, y1)


def stabilize(anchors, cell_hint):
    """
    Sustituye anclas atipicas por la mediana. Protege de fotogramas donde un
    efecto toca el suelo y contamina la banda de los pies.
    """
    if not anchors:
        return anchors
    xs = sorted(a[0] for a in anchors)
    median = xs[len(xs) // 2]
    limit = max(12, int(cell_hint * 0.10))
    return [(median if abs(x - median) > limit else x, y) for (x, y) in anchors]


def main():
    if not os.path.isdir(SRC_ROOT):
        sys.exit(f"No encuentro la carpeta de sprites: {SRC_ROOT}")

    os.makedirs(OUT_DIR, exist_ok=True)
    os.makedirs(PREVIEW_DIR, exist_ok=True)

    groups = collect()

    # Sin argumentos se rehace todo. Con nombres, solo esos: el manifiesto se
    # parchea y las demas hojas se quedan como estan, que reimportar las 21 en
    # Unity por tocar una es media hora tirada.
    pedidas = {a.strip().lower() for a in sys.argv[1:] if a.strip()}
    if pedidas:
        raras = pedidas - set(groups)
        if raras:
            sys.exit("No conozco estas animaciones: " + ", ".join(sorted(raras)))
        groups = {k: v for k, v in groups.items() if k in pedidas}
        print(f"{len(groups)} animaciones pedidas (el resto se deja intacto)\n")
    else:
        print(f"{len(groups)} animaciones detectadas\n")

    manifest = {"scale": SCALE, "animations": []}
    total_px = 0

    for key in sorted(groups):
        parts = groups[key]
        frames = []          # [(PIL.Image recortada, anchor)]

        for _part, tps_path in parts:
            # Primero, los PNG sueltos que hay en esa misma carpeta: son los
            # fotogramas tal y como los exporto el dibujante, y son la verdad.
            sueltos = frames_de_carpeta(os.path.dirname(tps_path))
            if sueltos:
                frames.extend(sueltos)
                continue

            print(f"  ~ {key}: sin PNG sueltos en {os.path.basename(os.path.dirname(tps_path))},"
                  f" tiro del .tpsheet")
            texture, size, entries = parse_tpsheet(tps_path)
            if not texture or not entries:
                print(f"  ! {key}: {os.path.basename(tps_path)} sin datos, se omite")
                continue

            folder = os.path.dirname(tps_path)
            png_path = os.path.join(folder, texture)
            if not os.path.exists(png_path):
                # Algunos .tpsheet declaran un :texture= equivocado (se quedo el
                # nombre de otra sesion de TexturePacker). Se busca el PNG real
                # que hay al lado.
                candidates = [f for f in os.listdir(folder) if f.endswith("_packer.png")]
                if len(candidates) == 1:
                    png_path = os.path.join(folder, candidates[0])
                    print(f"  ~ {key}: '{texture}' no existe, uso '{candidates[0]}'")
                else:
                    print(f"  ! {key}: falta {texture} y no hay alternativa clara, se omite")
                    continue

            sheet = Image.open(png_path).convert("RGBA")
            sheet_h = sheet.size[1] if not size else size[1]

            for name, x, y, w, h in sorted(entries, key=lambda e: frame_sort_key(e[0])):
                # TexturePacker mide Y desde arriba, PIL tambien -> recorte directo
                box = (x, y, x + w, y + h)
                if box[2] > sheet.size[0] or box[3] > sheet.size[1]:
                    continue
                frames.append(sheet.crop(box))
            _ = sheet_h

        if not frames:
            print(f"  ! {key}: sin fotogramas")
            continue

        tope = MAX_FRAMES.get(key)
        if tope and len(frames) > tope:
            paso = [int(round(i * (len(frames) - 1) / (tope - 1))) for i in range(tope)]
            frames = [frames[i] for i in sorted(set(paso))]

        # --- escalado y anclaje
        scaled = []
        for im in frames:
            w = max(1, int(round(im.size[0] * SCALE)))
            h = max(1, int(round(im.size[1] * SCALE)))
            im = im.resize((w, h), Image.LANCZOS)
            anchor = alpha_anchor(im)
            if anchor is None:
                anchor = (w // 2, h)
            scaled.append((im, anchor))

        if key not in SIN_ESTABILIZAR:
            widest = max(im.size[0] for im, _ in scaled)
            fixed = stabilize([a for _, a in scaled], widest)
            scaled = [(im, fixed[i]) for i, (im, _) in enumerate(scaled)]

        # celda: hay que poder meter cualquier fotograma con su ancla centrada
        left = max(a[0] for _, a in scaled)
        right = max(im.size[0] - a[0] for im, a in scaled)
        up = max(a[1] for _, a in scaled)
        cell_w = left + right + SIDE_MARGIN * 2
        cell_h = up + BOTTOM_MARGIN
        cell_w += cell_w % 2
        cell_h += cell_h % 2

        anchor_x = left + SIDE_MARGIN
        anchor_y = cell_h - BOTTOM_MARGIN

        # --- rejilla
        n = len(scaled)
        cols = min(MAX_COLS, n)
        rows = math.ceil(n / cols)
        sheet_img = Image.new("RGBA", (cols * cell_w, rows * cell_h), (0, 0, 0, 0))
        onion = Image.new("RGBA", (cell_w, cell_h), (0, 0, 0, 0))

        for i, (im, (ax, ay)) in enumerate(scaled):
            cx = (i % cols) * cell_w + anchor_x - ax
            cy = (i // cols) * cell_h + anchor_y - ay
            sheet_img.paste(im, (cx, cy), im)

            ghost = im.copy()
            ghost.putalpha(ghost.getchannel("A").point(lambda v: int(v * 0.18)))
            onion.alpha_composite(ghost, (anchor_x - ax, anchor_y - ay))

        out_png = os.path.join(OUT_DIR, f"ekkar_{key}.png")
        sheet_img.save(out_png, optimize=True)
        onion.save(os.path.join(PREVIEW_DIR, f"{key}.png"))

        px = cols * cell_w * rows * cell_h
        total_px += px

        manifest["animations"].append({
            "name": key,
            "file": f"ekkar_{key}.png",
            "frames": n,
            "cols": cols,
            "rows": rows,
            "cellWidth": cell_w,
            "cellHeight": cell_h,
            "pivotX": round(anchor_x / cell_w, 6),
            "pivotY": round((cell_h - anchor_y) / cell_h, 6),
            "fps": FPS.get(key, DEFAULT_FPS),
            "loop": key in ("idle", "run"),
        })

        print(f"  {key:34s} {n:3d} fot.  celda {cell_w}x{cell_h}  hoja {cols}x{rows}")

    manifest_path = os.path.join(OUT_DIR, "ekkar_anim_manifest.json")

    if pedidas and os.path.exists(manifest_path):
        with open(manifest_path, "r", encoding="utf-8") as f:
            anterior = json.load(f)
        nuevas = {a["name"]: a for a in manifest["animations"]}
        fundido = [nuevas.pop(a["name"], a) for a in anterior.get("animations", [])]
        fundido.extend(nuevas.values())
        manifest["animations"] = fundido

    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)

    print(f"\ntotal {sum(a['frames'] for a in manifest['animations'])} fotogramas")
    print(f"VRAM sin comprimir aprox: {total_px * 4 / 1024 / 1024:.0f} MB")
    print(f"hojas -> {OUT_DIR}")
    print(f"revision visual -> {PREVIEW_DIR}")


if __name__ == "__main__":
    main()
