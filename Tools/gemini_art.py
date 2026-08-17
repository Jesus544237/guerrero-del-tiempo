# -*- coding: utf-8 -*-
"""
Generador de arte para "Guerrero del Tiempo" usando la API de Gemini.

Uso rapido
----------
  python gemini_art.py --list
  python gemini_art.py --prompt-file prompts/medieval/00_cielo.txt --out out/00_cielo.png
  python gemini_art.py --prompt "..." --ref refs/ekkar.png --out out/enemigo.png
  python gemini_art.py --prompt-file p.txt --out out/capa.png --chroma   (recorta el fondo magenta)

La clave se lee, en este orden, de:
  1. la variable de entorno GEMINI_API_KEY
  2. el archivo Tools/gemini_key.txt (una sola linea)

NO subas gemini_key.txt a ningun repositorio ni lo pegues en un chat.
"""

import argparse
import base64
import json
import os
import sys
import time
import urllib.error
import urllib.request

BASE = "https://generativelanguage.googleapis.com/v1beta"
HERE = os.path.dirname(os.path.abspath(__file__))


# --------------------------------------------------------------------- clave

def load_key():
    key = os.environ.get("GEMINI_API_KEY", "").strip()
    if key:
        return key

    path = os.path.join(HERE, "gemini_key.txt")
    if os.path.exists(path):
        with open(path, "r", encoding="utf-8") as f:
            key = f.read().strip()
        if key:
            return key

    sys.exit(
        "No encuentro la clave de Gemini.\n"
        "  - Crea el archivo Tools/gemini_key.txt con la clave dentro, o\n"
        "  - define la variable de entorno GEMINI_API_KEY.\n"
        "Puedes obtenerla en https://aistudio.google.com/apikey"
    )


def post(url, payload, key, timeout=300, soft_fail=False):
    """soft_fail=True devuelve None en errores 4xx en vez de abortar."""
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        url,
        data=data,
        headers={"Content-Type": "application/json", "x-goog-api-key": key},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", "replace")
        if soft_fail and 400 <= e.code < 500:
            return None
        sys.exit(f"Error HTTP {e.code} en {url.split('/')[-1]}:\n{body[:2000]}")


def get(url, key, timeout=60):
    req = urllib.request.Request(url, headers={"x-goog-api-key": key}, method="GET")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", "replace")
        sys.exit(f"Error HTTP {e.code}:\n{body[:2000]}")


# -------------------------------------------------------------------- modelos

def list_models(key):
    """Muestra los modelos disponibles para esta clave, marcando los de imagen."""
    out = get(f"{BASE}/models?pageSize=200", key)
    models = out.get("models", [])
    image_like, others = [], []
    for m in models:
        name = m.get("name", "").replace("models/", "")
        methods = m.get("supportedGenerationMethods", [])
        if "predict" in methods or "image" in name.lower():
            image_like.append((name, methods))
        else:
            others.append((name, methods))

    print("== MODELOS QUE PUEDEN GENERAR IMAGENES ==")
    for name, methods in image_like:
        print(f"  {name}   [{', '.join(methods)}]")
    if not image_like:
        print("  (ninguno: probablemente la clave no tiene habilitada la generacion de imagenes)")

    print("\n== OTROS MODELOS ==")
    for name, methods in others[:40]:
        print(f"  {name}")
    return [n for n, _ in image_like]


def pick_model(key, requested, has_refs):
    if requested:
        return requested
    names = []
    out = get(f"{BASE}/models?pageSize=200", key)
    for m in out.get("models", []):
        names.append(m.get("name", "").replace("models/", ""))

    # Gemini 3 Pro Image es el mejor para pixel art detallado y ademas acepta
    # imagenes de referencia, asi que sirve para fondos y para personajes.
    prefer = ["gemini-3-pro-image", "gemini-3.1-flash-image",
              "gemini-3-pro-image-preview", "gemini-2.5-flash-image"]
    _ = has_refs

    for p in prefer:
        for n in names:
            if n.startswith(p):
                return n
    for n in names:
        if "image" in n:
            return n
    sys.exit("No encuentro ningun modelo de imagen. Ejecuta --list para ver los disponibles.")


# ------------------------------------------------------------------ generacion

def mime_of(path):
    ext = os.path.splitext(path)[1].lower()
    return {"png": "image/png", "jpg": "image/jpeg", "jpeg": "image/jpeg",
            "webp": "image/webp"}.get(ext.lstrip("."), "image/png")


def gen_gemini(model, prompt, refs, key, aspect="16:9", size="2K"):
    parts = [{"text": prompt}]
    for r in refs:
        with open(r, "rb") as f:
            parts.append({"inline_data": {
                "mime_type": mime_of(r),
                "data": base64.b64encode(f.read()).decode("ascii"),
            }})

    payload = {
        "contents": [{"role": "user", "parts": parts}],
        "generationConfig": {
            "responseModalities": ["IMAGE"],
            "imageConfig": {"aspectRatio": aspect, "imageSize": size},
        },
    }

    url = f"{BASE}/models/{model}:generateContent"
    out = post(url, payload, key, soft_fail=True)

    # los modelos mas antiguos no aceptan imageConfig: se reintenta sin el
    if out is None:
        payload["generationConfig"] = {"responseModalities": ["IMAGE"]}
        out = post(url, payload, key, soft_fail=True)
    if out is None:
        payload.pop("generationConfig", None)
        out = post(url, payload, key)

    images, texts = [], []
    for cand in out.get("candidates", []):
        for part in cand.get("content", {}).get("parts", []):
            blob = part.get("inlineData") or part.get("inline_data")
            if blob and blob.get("data"):
                images.append(base64.b64decode(blob["data"]))
            elif part.get("text"):
                texts.append(part["text"])

    if not images:
        msg = "\n".join(texts)[:1500] or json.dumps(out)[:1500]
        sys.exit(f"El modelo no devolvio ninguna imagen. Respuesta:\n{msg}")
    return images


def gen_imagen(model, prompt, key, aspect, count):
    payload = {
        "instances": [{"prompt": prompt}],
        "parameters": {"sampleCount": count, "aspectRatio": aspect},
    }
    out = post(f"{BASE}/models/{model}:predict", payload, key)
    images = []
    for pred in out.get("predictions", []):
        b64 = pred.get("bytesBase64Encoded") or pred.get("image", {}).get("bytesBase64Encoded")
        if b64:
            images.append(base64.b64decode(b64))
    if not images:
        sys.exit(f"Imagen no devolvio nada:\n{json.dumps(out)[:1500]}")
    return images


# ------------------------------------------------------------- postproceso

def chroma_key(path, tolerance=70):
    """Convierte el fondo verde croma (#00FF00) en transparencia."""
    try:
        from PIL import Image
    except ImportError:
        print("  (aviso: sin Pillow no puedo recortar el fondo)")
        return
    im = Image.open(path).convert("RGBA")
    px = im.load()
    w, h = im.size
    cut = 0
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            # verde dominante: mucho verde y poco rojo/azul
            if g > 255 - tolerance and r < tolerance and b < tolerance:
                px[x, y] = (r, g, b, 0)
                cut += 1
    im.save(path)
    print(f"  fondo verde recortado ({100.0 * cut / (w * h):.1f}%) -> {path}")


def main():
    ap = argparse.ArgumentParser(description="Generador de arte con Gemini para Guerrero del Tiempo")
    ap.add_argument("--list", action="store_true", help="lista los modelos disponibles")
    ap.add_argument("--prompt", help="texto del prompt")
    ap.add_argument("--prompt-file", help="archivo .txt con el prompt")
    ap.add_argument("--ref", action="append", default=[], help="imagen de referencia (repetible)")
    ap.add_argument("--out", help="ruta del PNG de salida")
    ap.add_argument("--model", help="forzar un modelo concreto")
    ap.add_argument("--aspect", default="16:9", help="relacion de aspecto")
    ap.add_argument("--size", default="2K", help="resolucion Gemini: 1K, 2K o 4K")
    ap.add_argument("--count", type=int, default=1, help="cuantas variantes generar")
    ap.add_argument("--chroma", action="store_true", help="recorta el fondo magenta a alfa")
    args = ap.parse_args()

    key = load_key()

    if args.list:
        list_models(key)
        return

    if not args.out:
        sys.exit("Falta --out")

    prompt = args.prompt
    if args.prompt_file:
        with open(args.prompt_file, "r", encoding="utf-8") as f:
            prompt = f.read().strip()
    if not prompt:
        sys.exit("Falta --prompt o --prompt-file")

    for r in args.ref:
        if not os.path.exists(r):
            sys.exit(f"No existe la referencia: {r}")

    model = pick_model(key, args.model, bool(args.ref))
    print(f"modelo: {model}")
    print(f"prompt: {prompt[:180]}{'...' if len(prompt) > 180 else ''}")
    if args.ref:
        print(f"referencias: {', '.join(os.path.basename(r) for r in args.ref)}")

    t0 = time.time()
    if model.startswith("imagen"):
        images = gen_imagen(model, prompt, key, args.aspect, args.count)
    else:
        images = []
        for _ in range(max(1, args.count)):
            images.extend(gen_gemini(model, prompt, args.ref, key, args.aspect, args.size))

    os.makedirs(os.path.dirname(os.path.abspath(args.out)) or ".", exist_ok=True)
    base, ext = os.path.splitext(args.out)
    written = []
    for i, data in enumerate(images):
        path = args.out if i == 0 else f"{base}_{i+1}{ext}"
        with open(path, "wb") as f:
            f.write(data)
        written.append(path)
        print(f"  escrito {path}  ({len(data)//1024} KB)")
        if args.chroma:
            chroma_key(path)

    print(f"listo en {time.time() - t0:.1f}s")


if __name__ == "__main__":
    main()
