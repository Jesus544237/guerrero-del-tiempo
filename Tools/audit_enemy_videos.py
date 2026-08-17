# -*- coding: utf-8 -*-
"""
Audita los clips de enemigos antes de montarlos.

Que mide, y por que cada cosa:

  - CROMA: cuanto fondo verde hay y si es plano. Si baja del 40 % o el verde
    varia mucho, el recorte va a salir sucio.
  - DERIVA: cuanto se mueve el centro del personaje y cuanto cambia su altura a
    lo largo del clip. El prompt pide que anime "en el sitio"; si el bicho cruza
    el encuadre o cambia de tamano, el sprite bailara en el juego.
  - BUCLE: para idle y caminar, busca el periodo y da una nota de lo bien que
    cierra el ciclo sobre si mismo.
  - TRAMOS: donde esta el movimiento, para saber si el clip trae la accion una
    vez, dos, o si se queda quieto media eternidad.
  - AUDIO: si hay pista, y donde estan los picos, para poder cortar el sonido
    en el mismo sitio que la animacion.
  - CONSISTENCIA: compara la paleta y las proporciones del personaje entre las
    animaciones de un mismo enemigo. Es lo que detecta que el idle y el ataque
    sean, en la practica, dos bichos distintos.

No decide nada: deja los numeros y las hojas de contacto para poder decidir.

Uso:
    python Tools/audit_enemy_videos.py
    python Tools/audit_enemy_videos.py --enemigo soldado_hueso
"""

import argparse
import json
import os
import re
import subprocess
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PROMPTS = os.path.join(HERE, "prompts")
OUT = os.path.join(HERE, "out", "auditoria")

VIDEO_EXT = (".mp4", ".mov", ".webm", ".mkv", ".m4v")
ANCHO = 320                     # resolucion de analisis
CLAVES = ("idle", "caminar", "ataque_especial", "ataque", "dano", "muerte",
          "portal", "transformacion", "fase2", "final")


# ------------------------------------------------------------------ hallazgo

def clave_anim(nombre, carpeta_padre):
    """Saca la animacion del nombre del archivo o, si no, de su carpeta."""
    for fuente in (nombre, carpeta_padre):
        base = re.sub(r"\.[a-z0-9]+$", "", fuente, flags=re.I).lower()
        base = re.sub(r"\s*\(\d+\)$", "", base)
        base = re.sub(r"^\d+[_\-\s]*", "", base)
        for k in CLAVES:
            if k in base:
                return k
    return None


def descubre(filtro_enemigo=None):
    items = []
    for era in sorted(os.listdir(PROMPTS)):
        raiz = os.path.join(PROMPTS, era, "enemigos")
        if not os.path.isdir(raiz):
            continue
        for enemigo in sorted(os.listdir(raiz)):
            d = os.path.join(raiz, enemigo)
            if not os.path.isdir(d) or enemigo.startswith("_"):
                continue
            if filtro_enemigo and enemigo != filtro_enemigo:
                continue
            for dirpath, _dirs, files in os.walk(d):
                rel = os.path.relpath(dirpath, d).replace("\\", "/")
                for fn in sorted(files):
                    if not fn.lower().endswith(VIDEO_EXT):
                        continue
                    ruta = os.path.join(dirpath, fn)
                    padre = os.path.basename(dirpath)

                    estado = "ok"
                    if "fallid" in rel.lower() or "descart" in rel.lower():
                        estado = "descartado"
                    elif rel == ".":
                        estado = "suelto"          # fuera de videos/
                    elif "aparte" in rel.lower():
                        estado = "aparte"

                    anim = clave_anim(fn, padre)
                    items.append(dict(era=era, enemigo=enemigo, anim=anim or "?",
                                      archivo=fn, ruta=ruta, subcarpeta=rel,
                                      estado=estado))
    return items


# -------------------------------------------------------------------- ffmpeg

def sonda(path):
    cmd = ["ffprobe", "-v", "error", "-show_entries",
           "stream=codec_type,width,height,avg_frame_rate:format=duration",
           "-of", "json", path]
    r = subprocess.run(cmd, capture_output=True, text=True, errors="replace")
    if r.returncode != 0:
        return None
    d = json.loads(r.stdout)
    info = dict(audio=False, w=0, h=0, fps=24.0,
                dur=float(d.get("format", {}).get("duration", 0) or 0))
    for s in d.get("streams", []):
        if s.get("codec_type") == "audio":
            info["audio"] = True
        elif s.get("codec_type") == "video":
            info["w"], info["h"] = int(s["width"]), int(s["height"])
            num, _, den = str(s.get("avg_frame_rate", "24/1")).partition("/")
            try:
                if float(den or 1) > 0:
                    info["fps"] = float(num) / float(den or 1)
            except ValueError:
                pass
    return info


def frames(path, w, h):
    lw = ANCHO
    lh = max(2, int(round(h * lw / float(w))))
    lw -= lw % 2
    lh -= lh % 2
    cmd = ["ffmpeg", "-v", "error", "-i", path, "-vf", f"scale={lw}:{lh}:flags=area",
           "-f", "rawvideo", "-pix_fmt", "rgb24", "-"]
    n = lw * lh * 3
    p = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, bufsize=n * 4)
    try:
        while True:
            buf = p.stdout.read(n)
            if not buf or len(buf) < n:
                break
            yield np.frombuffer(buf, np.uint8).reshape(lh, lw, 3)
    finally:
        p.stdout.close()
        p.wait()


def envolvente_audio(path, dur):
    """RMS por decima de segundo, normalizado. Para saber donde suena que."""
    cmd = ["ffmpeg", "-v", "error", "-i", path, "-ac", "1", "-ar", "8000",
           "-f", "s16le", "-"]
    r = subprocess.run(cmd, capture_output=True)
    if r.returncode != 0 or not r.stdout:
        return []
    x = np.frombuffer(r.stdout, np.int16).astype(np.float32)
    if x.size == 0:
        return []
    paso = 800                                     # 0.1 s a 8 kHz
    n = x.size // paso
    if n == 0:
        return []
    env = np.sqrt((x[:n * paso].reshape(n, paso) ** 2).mean(axis=1))
    m = env.max()
    return (env / m).round(3).tolist() if m > 0 else []


# ------------------------------------------------------------------ analisis

def analiza(path, info):
    verdes, cajas, thumbs, paletas = [], [], [], []

    for rgb in frames(path, info["w"], info["h"]):
        r = rgb[..., 0].astype(np.int16)
        g = rgb[..., 1].astype(np.int16)
        b = rgb[..., 2].astype(np.int16)
        fondo = (g - np.maximum(r, b) > 45) & (g > 70)
        verdes.append(float(fondo.mean()))

        obj = ~fondo
        if obj.sum() < 40:
            cajas.append(None)
            thumbs.append(np.zeros(48 * 48, np.float32))
            continue

        ys = np.flatnonzero(obj.any(axis=1))
        xs = np.flatnonzero(obj.any(axis=0))
        cajas.append((int(xs[0]), int(ys[0]), int(xs[-1]) + 1, int(ys[-1]) + 1))

        gris = rgb.astype(np.float32).mean(axis=2) * obj
        thumbs.append(np.asarray(Image.fromarray(gris.astype(np.uint8)).resize((48, 48), Image.BILINEAR),
                                 dtype=np.float32).ravel())
        # paleta: histograma grueso del color del personaje
        q = (rgb[obj] // 64).astype(np.int32)
        idx = q[:, 0] * 16 + q[:, 1] * 4 + q[:, 2]
        paletas.append(np.bincount(idx, minlength=64).astype(np.float32))

    if not verdes:
        return None

    llenos = [c for c in cajas if c]
    res = dict(nframes=len(verdes),
               verde_medio=round(float(np.mean(verdes)), 3),
               verde_min=round(float(np.min(verdes)), 3))

    if llenos:
        cx = np.array([(c[0] + c[2]) / 2 for c in llenos])
        alt = np.array([c[3] - c[1] for c in llenos])
        anc = np.array([c[2] - c[0] for c in llenos])
        base = np.array([c[3] for c in llenos])
        res.update(
            deriva_x=round(float(cx.max() - cx.min()), 1),
            alto_medio=round(float(np.median(alt)), 1),
            alto_var=round(float((alt.max() - alt.min()) / max(1, np.median(alt))), 3),
            ancho_medio=round(float(np.median(anc)), 1),
            base_var=round(float(base.max() - base.min()), 1),
            vacios=int(len(cajas) - len(llenos)),
        )
        P = np.stack(paletas)
        P = P / np.maximum(1e-6, P.sum(axis=1, keepdims=True))
        res["paleta"] = P.mean(axis=0).round(4).tolist()

    A = np.stack(thumbs)
    if len(A) >= 10:
        # movimiento entre fotogramas y distancia a la pose neutra
        mov = np.abs(np.diff(A, axis=0)).mean(axis=1)
        res["movimiento"] = (mov / max(1e-6, mov.max())).round(3).tolist()
        neutra = np.median(A, axis=0)
        d = np.abs(A - neutra).mean(axis=1)
        res["desvio"] = (d / max(1e-6, d.max())).round(3).tolist()

        # mejor periodo de bucle y como de bien cierra
        lo, hi = max(4, len(A) // 12), max(5, len(A) // 2)
        mejor, per = None, 0
        for p in range(lo, hi + 1):
            v = float(np.abs(A[:-p] - A[p:]).mean())
            if mejor is None or v < mejor:
                mejor, per = v, p
        escala = float(np.abs(A - A.mean(axis=0)).mean()) or 1.0
        res["periodo"] = per
        res["bucle_error"] = round(mejor / escala, 3)      # 0 = ciclo perfecto

    return res


def hoja_contacto(path, info, destino, n=12):
    lado = 300
    vf = (f"select='not(mod(n\\,{max(1, int(info['nframes_est'] // n))}))',"
          f"scale={lado}:-2,tile=4x3")
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-i", path, "-vf", vf,
                    "-frames:v", "1", destino], capture_output=True)


# ---------------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser(description="Audita los clips de enemigos.")
    ap.add_argument("--enemigo")
    ap.add_argument("--sin-hojas", action="store_true", dest="sin_hojas")
    args = ap.parse_args()

    os.makedirs(OUT, exist_ok=True)
    items = descubre(args.enemigo)
    print(f"{len(items)} clips encontrados\n")

    for i, it in enumerate(items, 1):
        info = sonda(it["ruta"])
        if not info or not info["w"]:
            it["error"] = "ffprobe no lo lee"
            print(f"  ! {it['enemigo']}/{it['archivo']}: ilegible")
            continue

        it.update(w=info["w"], h=info["h"], fps=round(info["fps"], 2),
                  dur=round(info["dur"], 2), audio=info["audio"])
        info["nframes_est"] = max(1, int(info["fps"] * info["dur"]))

        res = analiza(it["ruta"], info)
        if res:
            it.update(res)
        if info["audio"]:
            it["audio_env"] = envolvente_audio(it["ruta"], info["dur"])

        if not args.sin_hojas and it["estado"] in ("ok", "suelto"):
            sub = os.path.join(OUT, it["era"], it["enemigo"])
            os.makedirs(sub, exist_ok=True)
            nombre = re.sub(r"[^\w.-]+", "_", os.path.splitext(it["archivo"])[0])[:60]
            destino = os.path.join(sub, f"{it['anim']}__{nombre}.jpg")
            hoja_contacto(it["ruta"], info, destino)
            it["hoja"] = os.path.relpath(destino, HERE).replace("\\", "/")

        print(f"  [{i:3d}/{len(items)}] {it['era']:10s} {it['enemigo']:22s} {it['anim']:16s} "
              f"{it.get('dur', 0):5.1f}s  verde {it.get('verde_medio', 0):.2f}  "
              f"deriva {it.get('deriva_x', 0):5.1f}  bucle {it.get('bucle_error', 0):.2f}"
              f"{'  [' + it['estado'] + ']' if it['estado'] != 'ok' else ''}")

    with open(os.path.join(OUT, "auditoria.json"), "w", encoding="utf-8") as f:
        json.dump(items, f, indent=1, ensure_ascii=False)

    print(f"\ndatos  -> {os.path.join(OUT, 'auditoria.json')}")
    print(f"hojas  -> {OUT}")


if __name__ == "__main__":
    main()
