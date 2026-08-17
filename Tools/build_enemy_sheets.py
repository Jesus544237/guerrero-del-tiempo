# -*- coding: utf-8 -*-
"""
Monta las hojas de sprites de los enemigos a partir de los clips de video.

Que hace, y por que:
  - Busca los clips en Tools/prompts/<era>/enemigos/<enemigo>/videos/, con el
    mismo nombre que el prompt que los genero (01_idle.mp4, 03_ataque.mp4...).
  - Saca los fotogramas con ffmpeg.
  - Recorta el croma verde #00FF00 con borde suave y quita el derrame verde
    que el video deja en el contorno del personaje.
  - Ignora la esquina inferior derecha, que los prompts dejan vacia a proposito
    porque ahi cae la marca de Gemini; si no, entraria en la hoja como un
    manchon opaco.
  - Elige el trozo util del clip: un ciclo suelto en las animaciones de bucle
    (idle, caminar) y una sola repeticion en las de un golpe (ataque, dano,
    muerte), y lo reduce al numero de fotogramas que pide la animacion.
  - Reancla por los pies, igual que Ekkar. Si el enemigo flota, ancla todos los
    fotogramas a una linea comun para no comerse el cabeceo.
  - Reescala cada enemigo a su altura canonica (Ekkar mide 240 px en pantalla).
  - Escribe la hoja + un manifiesto JSON con el mismo formato que el de Ekkar,
    para que el importador de Unity sea el mismo de siempre.
  - Deja una "cebolla" (fotogramas superpuestos) y una tira sobre cuadriculado
    en Tools/out/preview_enemigos/ para revisar el anclaje de un vistazo.

Necesita ffmpeg y ffprobe en el PATH.

Uso:
    python Tools/build_enemy_sheets.py                      # todo lo que encuentre
    python Tools/build_enemy_sheets.py --listar             # solo dice que ve
    python Tools/build_enemy_sheets.py --enemigo soldado_hueso
    python Tools/build_enemy_sheets.py --era medieval
    python Tools/build_enemy_sheets.py --enemigo soldado_hueso --anim 03_ataque \
        --fotogramas 12 --desde 1.4 --hasta 3.2
"""

import argparse
import json
import math
import os
import re
import subprocess
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(HERE)
PROMPTS_DIR = os.path.join(HERE, "prompts")
ART_DIR = os.path.join(PROJ, "Assets", "_Game", "Art", "Characters", "Enemigos")
PREVIEW_DIR = os.path.join(HERE, "out", "preview_enemigos")

VIDEO_EXT = (".mp4", ".mov", ".webm", ".mkv", ".m4v", ".avi")

# clips que estan en la carpeta pero no son animaciones del personaje
IGNORAR = ("parecido", "armored_character", "referencia", "prueba")

# Cortes revisados a mano, en segundos: {enemigo: {animacion: [desde, hasta]}}
# La deteccion automatica acierta en la mayoria, pero no en todos, y un clip
# mal cortado se nota en el juego mas que cualquier otra cosa. Lo que se decide
# mirando queda escrito aqui y manda sobre la deteccion.
CORTES_PATH = os.path.join(HERE, "enemy_cuts.json")


def carga_cortes():
    if not os.path.exists(CORTES_PATH):
        return {}
    try:
        with open(CORTES_PATH, encoding="utf-8") as f:
            return json.load(f)
    except ValueError:
        print(f"  ! {CORTES_PATH} no es JSON valido, lo ignoro")
        return {}

CORTES = {}
EKKAR_PX = 240          # altura canonica de Ekkar en pantalla a 1080p
BOTTOM_MARGIN = 4       # px libres bajo los pies, dentro de la celda
SIDE_MARGIN = 6
MAX_COLS = 8
ANALISIS_W = 320        # ancho de la pasada rapida de analisis

# Altura en pantalla de cada enemigo y si flota. Sale de su propio prompt:
# "slightly shorter than the hero", "two thirds the hero's height", etc.
# Enemigos normales 180-220, mini-jefes 300-360, el Senor del Tiempo ~media
# pantalla. Se puede pisar con --altura.
# 'voltea' pone al bicho mirando a la IZQUIERDA. Gemini los genero a todos
# mirando a la derecha, igual que Ekkar, y en el juego Ekkar avanza hacia la
# derecha: sin voltearlos se darian la espalda. El jefe no se voltea porque
# esta de frente.
ROSTER = {
    # medieval
    "soldado_hueso":         dict(alto=215, vuela=False, voltea=True),
    "espectro_asedio":       dict(alto=235, vuela=True,  voltea=True),
    "ballestero_ceniciento": dict(alto=220, vuela=False, voltea=True),
    "caballero_ceniciento":  dict(alto=340, vuela=False, voltea=True),   # mini-jefe
    # industrial
    "automata_fundicion":    dict(alto=210, vuela=False, voltea=True),
    "dron_vapor":            dict(alto=160, vuela=True,  voltea=True),
    "capataz_oxidado":       dict(alto=265, vuela=False, voltea=True),
    "el_gran_yunque":        dict(alto=340, vuela=False, voltea=True),   # mini-jefe
    # futuro
    "dron_centinela":        dict(alto=120, vuela=True,  voltea=True),
    "corredor_datos":        dict(alto=240, vuela=False, voltea=True),
    "torreta_holografica":   dict(alto=165, vuela=False, voltea=True),
    "nemesis_digital":       dict(alto=240, vuela=False, voltea=True),   # mini-jefe
    # hora cero
    "eco_de_ekkar":          dict(alto=240, vuela=False, voltea=True),
    "fragmento_animado":     dict(alto=120, vuela=True,  voltea=True),
    "senor_del_tiempo":      dict(alto=520, vuela=True,  voltea=False),  # jefe final, de frente
}

# El sonido se guarda solo en las animaciones de un golpe: un efecto de ataque
# o de muerte suena una vez y encaja. En idle y caminar seria un bucle corto
# repitiendose sin parar, que es de lo primero que cansa en un juego.
AUDIO_EN = ("ataque", "dano", "muerte", "especial", "portal", "transformacion", "final")

# La animacion de referencia fija la altura del personaje: se mide ahi y el
# mismo factor se aplica a todas las demas, para que no cambie de talla entre
# animaciones.
REF_ANIMS = ("idle", "idle_fase1", "idle_fase2", "caminar")


def perfil(anim):
    """(fotogramas, fps, bucle) segun el tipo de animacion."""
    if "idle" in anim:           return 8, 10, True
    if "caminar" in anim:        return 8, 10, True
    if "transformacion" in anim: return 16, 14, False
    if "portal" in anim:         return 14, 14, False
    if "muerte" in anim:         return 12, 12, False
    if "dano" in anim:           return 6, 14, False
    if "especial" in anim:       return 12, 14, False
    if "ataque" in anim:         return 10, 14, False
    return 10, 12, False


# --------------------------------------------------------------------- ffmpeg

def _run(cmd):
    return subprocess.run(cmd, capture_output=True, text=True, errors="replace")


def comprueba_ffmpeg():
    for exe in ("ffmpeg", "ffprobe"):
        try:
            _run([exe, "-version"])
        except FileNotFoundError:
            sys.exit(f"Falta {exe} en el PATH. Instalalo con:  winget install ffmpeg")


def info_video(path):
    """(ancho, alto, fps) del clip."""
    cmd = ["ffprobe", "-v", "error", "-select_streams", "v:0",
           "-show_entries", "stream=width,height,avg_frame_rate,r_frame_rate",
           "-of", "json", path]
    res = _run(cmd)
    if res.returncode != 0:
        raise RuntimeError(f"ffprobe fallo en {path}: {res.stderr.strip()}")
    st = json.loads(res.stdout)["streams"][0]

    fps = 24.0
    for key in ("avg_frame_rate", "r_frame_rate"):
        val = st.get(key, "0/0")
        num, _, den = val.partition("/")
        try:
            num, den = float(num), float(den or 1)
        except ValueError:
            continue
        if den > 0 and num > 0:
            fps = num / den
            break

    return int(st["width"]), int(st["height"]), fps


def fotogramas(path, w, h, escala=None):
    """Va soltando los fotogramas del clip como arrays RGB (alto, ancho, 3)."""
    cmd = ["ffmpeg", "-v", "error", "-i", path]
    if escala:
        w, h = escala
        cmd += ["-vf", f"scale={w}:{h}:flags=area"]
    cmd += ["-f", "rawvideo", "-pix_fmt", "rgb24", "-"]

    n = w * h * 3
    proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                            bufsize=n * 4)
    try:
        while True:
            buf = proc.stdout.read(n)
            if buf is None or len(buf) < n:
                break
            yield np.frombuffer(buf, np.uint8).reshape(h, w, 3)
    finally:
        proc.stdout.close()
        proc.wait()


# ---------------------------------------------------------------------- croma

def recorta_croma(rgb, bajo, alto, marca):
    """
    Convierte un fotograma RGB en RGBA quitando el fondo verde.

    El fondo es #00FF00 plano, asi que basta con mirar cuanto se pasa el verde
    por encima del rojo y el azul. El borde sale suave porque el video comprime
    y ahi el verde se mezcla con el personaje; ese mismo borde se limpia
    despues quitando el derrame.
    """
    r = rgb[..., 0].astype(np.int16)
    g = rgb[..., 1].astype(np.int16)
    b = rgb[..., 2].astype(np.int16)

    exceso = g - np.maximum(r, b)
    fondo = np.clip((exceso - bajo) / float(max(1, alto - bajo)), 0.0, 1.0)
    fondo[g < 70] = 0.0                      # el croma es verde puro y brillante

    a = ((1.0 - fondo) * 255.0).astype(np.uint8)

    # derrame: donde el verde sigue mandando, se le baja a la media de los
    # otros dos canales. Es el truco de siempre y aqui es gratis, porque el
    # prompt prohibe el verde encima del personaje.
    media = ((r + b) // 2).astype(np.int16)
    g2 = np.where(g > media, media, g).astype(np.uint8)

    out = np.dstack([rgb[..., 0], g2, rgb[..., 2], a])

    # la esquina de la marca de agua no entra en la hoja
    if marca:
        fw, fh = marca
        y0 = int(out.shape[0] * (1.0 - fh))
        x0 = int(out.shape[1] * (1.0 - fw))
        out[y0:, x0:, 3] = 0

    return out


def limpia_motas(a):
    """Quita pixeles opacos sueltos: ruido de compresion en el croma."""
    op = (a > 40).astype(np.uint8)
    vecinos = np.zeros(op.shape, np.uint8)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dx == 0 and dy == 0:
                continue
            vecinos += np.roll(np.roll(op, dy, 0), dx, 1)
    a = a.copy()
    a[(op == 1) & (vecinos < 3)] = 0
    a[a < 16] = 0                            # el halo de croma no vale nada
    return a


def caja(a, umbral=40):
    """Caja del contenido opaco, o None si el fotograma esta vacio."""
    mask = a > umbral
    if not mask.any():
        return None
    ys = np.flatnonzero(mask.any(axis=1))
    xs = np.flatnonzero(mask.any(axis=0))
    return int(xs[0]), int(ys[0]), int(xs[-1]) + 1, int(ys[-1]) + 1


# -------------------------------------------------------------------- analisis

def analiza(path, w, h, args):
    """
    Pasada rapida a baja resolucion. Devuelve por fotograma su caja, su area y
    una miniatura, que es con lo que luego se decide que trozo del clip vale.
    """
    lw = min(ANALISIS_W, w)
    lh = max(2, int(round(h * lw / float(w))))
    lw -= lw % 2
    lh -= lh % 2

    stats = []
    for rgb in fotogramas(path, w, h, escala=(lw, lh)):
        rgba = recorta_croma(rgb, args.umbral_bajo, args.umbral_alto, args.marca)
        a = rgba[..., 3]
        bb = caja(a, umbral=60)
        gris = (rgba[..., :3].astype(np.float32).mean(axis=2) * (a / 255.0))
        thumb = np.asarray(Image.fromarray(gris.astype(np.uint8)).resize((48, 48), Image.BILINEAR),
                           dtype=np.float32).ravel()
        stats.append(dict(bbox=bb, area=int((a > 60).sum()), thumb=thumb))
    return stats


def periodo(thumbs):
    """
    Periodo del ciclo.

    Ojo con quedarse con el minimo a secas: si el ciclo dura P, entonces 2P y
    3P tambien encajan casi igual de bien, y el minimo global cae muchas veces
    en un multiplo. Eso mete dos zancadas o dos espadazos en la misma
    animacion. Por eso se busca el periodo MAS CORTO que este a la altura del
    mejor, no el mejor a secas.
    """
    n = len(thumbs)
    if n < 10:
        return n
    A = np.stack(thumbs)
    lo = max(4, n // 20)
    hi = max(lo + 1, n // 2)

    ds = {p: float(np.abs(A[:-p] - A[p:]).mean()) for p in range(lo, hi + 1)}
    mejor = min(ds.values())
    peor = max(ds.values())
    margen = mejor + (peor - mejor) * 0.15          # "tan bueno como el mejor"
    return min(p for p, d in ds.items() if d <= margen)


def tramo_bucle(stats):
    """Un ciclo limpio, cogido del centro del clip."""
    llenos = [i for i, s in enumerate(stats) if s["bbox"] is not None]
    if not llenos:
        return 0, len(stats)
    ini, fin = llenos[0], llenos[-1] + 1
    thumbs = [stats[i]["thumb"] for i in range(ini, fin)]
    p = periodo(thumbs)
    if p >= len(thumbs):
        return ini, fin

    # de los ciclos posibles se coge el que mejor cierra sobre si mismo, y de
    # los del centro, que es donde el clip esta mas asentado
    A = np.stack(thumbs)
    opciones = range(max(0, (len(thumbs) - p) // 4), max(1, len(thumbs) - p))
    s = min(opciones, key=lambda k: float(np.abs(A[k] - A[k + p]).mean()))
    return ini + s, ini + s + p


def tramo_golpe(stats, hasta_el_final=False):
    """
    Una sola repeticion.

    Los clips traen la accion 2 o 3 veces seguidas, asi que lo primero que se
    intenta es medir el periodo: si el clip se repite de verdad, un periodo ES
    una repeticion, y se coge empezando por el fotograma mas parecido a la pose
    neutra, para que la animacion lea neutra -> golpe -> neutra.

    Si no hay periodo claro (una muerte no se repite: ocurre una vez y el bicho
    se queda en el suelo), se cae al plan B: mirar cuanto se aleja cada
    fotograma de la pose neutra. No sirve mirar el movimiento entre fotogramas,
    porque los prompts piden que el golpe se quede quieto un instante en la
    extension maxima, y ahi el movimiento baja a cero y parte la accion en dos.
    """
    llenos = [i for i, s in enumerate(stats) if s["bbox"] is not None]
    if not llenos:
        return 0, len(stats)
    ini, fin = llenos[0], llenos[-1] + 1

    if not hasta_el_final:
        A = np.stack([stats[i]["thumb"] for i in range(ini, fin)])
        if len(A) >= 12:
            p = periodo(list(A))
            # se compara con la diferencia media entre fotogramas cualesquiera:
            # si repetirse cada p fotogramas es mucho mejor que eso, el clip es
            # ciclico de verdad y no es casualidad
            d_ciclo = float(np.abs(A[:-p] - A[p:]).mean())
            d_media = float(np.abs(A - A.mean(axis=0)).mean()) * 2.0
            if d_media > 0 and d_ciclo / d_media < 0.62 and p < len(A) * 0.6:
                neutra = np.median(A, axis=0)
                dev = np.abs(A - neutra).mean(axis=1)
                # arranca en el fotograma mas neutro de la primera mitad
                arranque = int(np.argmin(dev[:max(1, len(A) - p)]))
                fin_ciclo = arranque + p

                if hasta_el_final:
                    # Una muerte tambien viene repetida, pero no puede volver a
                    # la pose de pie: se corta en cuanto el bicho lleva un rato
                    # sin moverse tumbado, o sea en el ultimo fotograma que
                    # sigue lejos de la pose neutra.
                    ventana = dev[arranque:fin_ciclo]
                    if ventana.size:
                        lejos = np.flatnonzero(ventana > ventana.max() * 0.45)
                        if lejos.size:
                            fin_ciclo = arranque + int(lejos[-1]) + 2
                return ini + arranque, ini + min(fin_ciclo, len(A))

    A = np.stack([stats[i]["thumb"] for i in range(ini, fin)])
    if len(A) < 4:
        return ini, fin

    neutra = np.median(A, axis=0)
    d = np.abs(A - neutra).mean(axis=1)
    if d.max() <= 0:
        return ini, fin
    d /= d.max()

    # histeresis: el tramo arranca cuando se despega de verdad de la pose
    # neutra y no se corta hasta que ha vuelto del todo
    alto, bajo = 0.30, 0.12
    tramos, arr = [], None
    for i, v in enumerate(d):
        if arr is None:
            if v > alto:
                arr = i
                while arr > 0 and d[arr - 1] > bajo:
                    arr -= 1
        elif v <= bajo:
            tramos.append((arr, i))
            arr = None
    if arr is not None:
        tramos.append((arr, len(d)))
    if not tramos:
        return ini, fin

    a, b = max(tramos, key=lambda t: t[1] - t[0])
    a = max(0, a - 2)                        # un poco de pose neutra por delante
    b = len(d) if hasta_el_final else min(len(d), b + 3)
    return ini + a, ini + b


def elige(stats, bucle, objetivo, desde, hasta, fps, final=False):
    """Indices de los fotogramas que acaban en la hoja."""
    n = len(stats)
    if desde is not None or hasta is not None:
        a = int(round((desde or 0) * fps))
        b = int(round(hasta * fps)) if hasta is not None else n
    elif bucle:
        a, b = tramo_bucle(stats)
    else:
        a, b = tramo_golpe(stats, hasta_el_final=final)

    a = max(0, min(a, n - 1))
    b = max(a + 1, min(b, n))

    idx = [i for i in range(a, b) if stats[i]["bbox"] is not None]
    if not idx:
        idx = list(range(a, b))
    if len(idx) <= objetivo:
        return idx

    # muestreo parejo; en bucle el ultimo fotograma no se coge, porque repite
    # el primero y en el juego se veria un tiron
    if bucle:
        pos = [int(round(k * len(idx) / float(objetivo))) for k in range(objetivo)]
    else:
        pos = [int(round(k * (len(idx) - 1) / float(objetivo - 1))) for k in range(objetivo)]
    vistos, sal = set(), []
    for p in pos:
        p = min(p, len(idx) - 1)
        if p not in vistos:
            vistos.add(p)
            sal.append(idx[p])
    return sal


# --------------------------------------------------------------------- anclaje

def ancla_pies(img):
    """
    Ancla = punto de apoyo. Se usa la mediana horizontal de la banda inferior
    de pixeles opacos, que es el calzado o la base: la media se desvia en
    cuanto un efecto ensancha la caja por un lado.
    """
    a = np.asarray(img)[..., 3]
    bb = caja(a)
    if bb is None:
        return None
    x0, y0, x1, y1 = bb
    banda = max(6, int((y1 - y0) * 0.12))
    trozo = a[max(y0, y1 - banda):y1, x0:x1] > 120
    xs = np.flatnonzero(trozo.any(axis=0))
    fx = x0 + int(np.median(xs)) if xs.size else (x0 + x1) // 2
    return fx, y1


def centro_opaco(img):
    a = np.asarray(img)[..., 3]
    bb = caja(a)
    if bb is None:
        return None
    x0, y0, x1, y1 = bb
    cols = (a[y0:y1, x0:x1] > 120).sum(axis=0)
    if cols.sum() == 0:
        return (x0 + x1) // 2, y1
    acum = np.cumsum(cols)
    return x0 + int(np.searchsorted(acum, acum[-1] / 2.0)), y1


def estabiliza(anclas, ancho_ref):
    """Las anclas atipicas se sustituyen por la mediana."""
    if not anclas:
        return anclas
    xs = sorted(a[0] for a in anclas)
    med = xs[len(xs) // 2]
    limite = max(12, int(ancho_ref * 0.10))
    return [(med if abs(x - med) > limite else x, y) for (x, y) in anclas]


# ------------------------------------------------------------------- imagenes

FILTROS = {"lanczos": Image.LANCZOS, "bicubico": Image.BICUBIC,
           "area": Image.BOX, "vecino": Image.NEAREST}


def escala_rgba(img, size, filtro):
    """
    Reescala premultiplicando el alfa. Sin esto el negro de las zonas
    transparentes se cuela en el contorno y el sprite sale con orla oscura.
    """
    arr = np.asarray(img).astype(np.float32)
    a = arr[..., 3:4] / 255.0
    arr[..., :3] *= a
    pre = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA").resize(size, filtro)

    out = np.asarray(pre).astype(np.float32)
    a2 = np.clip(out[..., 3:4] / 255.0, 1e-3, 1.0)
    out[..., :3] = np.clip(out[..., :3] / a2, 0, 255)
    out[..., 3][out[..., 3] < 16] = 0
    return Image.fromarray(out.astype(np.uint8), "RGBA")


def cuadricula(size, paso=16):
    """Fondo a cuadros para poder ver la transparencia en la tira de revision."""
    w, h = size
    base = Image.new("RGBA", size, (26, 22, 38, 255))
    claro = Image.new("RGBA", (paso, paso), (44, 38, 62, 255))
    for y in range(0, h, paso):
        for x in range(0, w, paso):
            if ((x // paso) + (y // paso)) % 2 == 0:
                base.paste(claro, (x, y))
    return base


# -------------------------------------------------------------------- extraer

def extrae_anim(path, args, objetivo, factor, filtro):
    """
    Devuelve (imagenes recortadas, desplazamiento de cada una, altura de
    referencia sin escalar, tramo de video elegido). Si factor es None solo
    mide, no escala.
    """
    w, h, fps = info_video(path)
    stats = analiza(path, w, h, args)
    if not stats:
        raise RuntimeError("el clip no dio ni un fotograma")

    bucle = args.bucle
    elegidos = elige(stats, bucle, objetivo, args.desde, args.hasta, fps,
                     final=getattr(args, 'final', False))
    idx = set(elegidos)
    if not idx:
        raise RuntimeError("no encontre fotogramas utiles")
    tramo = (min(elegidos) / fps, (max(elegidos) + 1) / fps)

    imgs, offs, altos = [], [], []
    for i, rgb in enumerate(fotogramas(path, w, h)):
        if i not in idx:
            continue
        rgba = recorta_croma(rgb, args.umbral_bajo, args.umbral_alto, args.marca)

        # correccion de tinte: algun clip sale con la paleta cambiada respecto
        # a las demas animaciones del mismo bicho (el Gran Yunque sale morado
        # en su ataque). Se corrige por canal en vez de regenerar el video.
        tinte = getattr(args, "tinte", None)
        if tinte:
            for i, k in enumerate(tinte[:3]):
                rgba[..., i] = np.clip(rgba[..., i].astype(np.float32) * float(k), 0, 255).astype(np.uint8)

        rgba[..., 3] = limpia_motas(rgba[..., 3])
        bb = caja(rgba[..., 3])
        if bb is None:
            continue
        x0, y0, x1, y1 = bb
        altos.append(y1 - y0)

        im = Image.fromarray(rgba[y0:y1, x0:x1], "RGBA")
        if factor is not None and abs(factor - 1.0) > 1e-3:
            nw = max(1, int(round(im.size[0] * factor)))
            nh = max(1, int(round(im.size[1] * factor)))
            im = escala_rgba(im, (nw, nh), filtro)
            x0, y0 = int(round(x0 * factor)), int(round(y0 * factor))
        imgs.append(im)
        offs.append((x0, y0))

    if not imgs:
        raise RuntimeError("todos los fotogramas salieron vacios")

    # Voltear al final, cuando ya esta recortado: asi el desplazamiento se
    # calcula en el mismo sistema que la imagen y el anclaje no se descoloca.
    if args.voltea:
        ancho_frame = int(round(w * (factor if factor else 1.0)))
        imgs = [im.transpose(Image.FLIP_LEFT_RIGHT) for im in imgs]
        offs = [(ancho_frame - (ox + im.size[0]), oy) for im, (ox, oy) in zip(imgs, offs)]

    ref = float(np.median(altos))
    return imgs, offs, ref, tramo


def corta_audio(path, tramo, destino):
    """Corta el sonido en el mismo tramo que la animacion. Devuelve el nivel."""
    t0, t1 = tramo
    cmd = ["ffmpeg", "-y", "-v", "error", "-ss", f"{t0:.3f}", "-i", path,
           "-t", f"{max(0.05, t1 - t0):.3f}", "-vn", "-ac", "1", "-ar", "44100",
           destino]
    if subprocess.run(cmd, capture_output=True).returncode != 0:
        return None
    if not os.path.exists(destino) or os.path.getsize(destino) < 1000:
        return None

    # nivel medio, para saber si lo que se corto es sonido o silencio
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", destino, "-f", "s16le",
                          "-ac", "1", "-ar", "8000", "-"], capture_output=True).stdout
    if not raw:
        return 0.0
    x = np.frombuffer(raw, np.int16).astype(np.float32) / 32768.0
    return round(float(np.sqrt((x ** 2).mean())), 4) if x.size else 0.0


def monta_hoja(imgs, offs, vuela):
    """
    Coloca los fotogramas en rejilla con el ancla siempre en el mismo sitio.
    Devuelve (hoja, cebolla, meta de la rejilla).
    """
    if vuela:
        # El cabeceo ES la animacion: se ancla todo a una linea comun (el punto
        # mas bajo del clip) y a un eje vertical fijo, asi el bicho sube y baja
        # dentro de la celda en vez de quedarse clavado.
        base_y = max(oy + im.size[1] for im, (ox, oy) in zip(imgs, offs))
        centros = []
        for im, (ox, _oy) in zip(imgs, offs):
            c = centro_opaco(im)
            centros.append(ox + (c[0] if c else im.size[0] // 2))
        eje = int(np.median(centros))
        anclas = [(eje - ox, base_y - oy) for (ox, oy) in offs]
    else:
        anclas = []
        for im in imgs:
            a = ancla_pies(im)
            anclas.append(a if a else (im.size[0] // 2, im.size[1]))
        anclas = estabiliza(anclas, max(im.size[0] for im in imgs))

    # la celda tiene que aceptar cualquier fotograma con su ancla en el sitio
    izq = max(ax for ax, _ in anclas)
    der = max(im.size[0] - ax for im, (ax, _) in zip(imgs, anclas))
    arr = max(ay for _, ay in anclas)
    abj = max(im.size[1] - ay for im, (_, ay) in zip(imgs, anclas))

    cell_w = izq + der + SIDE_MARGIN * 2
    cell_h = arr + max(BOTTOM_MARGIN, abj)
    cell_w += cell_w % 2
    cell_h += cell_h % 2

    anchor_x = izq + SIDE_MARGIN
    anchor_y = cell_h - max(BOTTOM_MARGIN, abj)

    n = len(imgs)
    cols = min(MAX_COLS, n)
    rows = math.ceil(n / cols)

    hoja = Image.new("RGBA", (cols * cell_w, rows * cell_h), (0, 0, 0, 0))
    cebolla = Image.new("RGBA", (cell_w, cell_h), (0, 0, 0, 0))

    for i, (im, (ax, ay)) in enumerate(zip(imgs, anclas)):
        cx = (i % cols) * cell_w + anchor_x - ax
        cy = (i // cols) * cell_h + anchor_y - ay
        hoja.paste(im, (cx, cy), im)

        fantasma = im.copy()
        fantasma.putalpha(fantasma.getchannel("A").point(lambda v: int(v * 0.18)))
        cebolla.alpha_composite(fantasma, (anchor_x - ax, anchor_y - ay))

    meta = dict(frames=n, cols=cols, rows=rows, cellWidth=cell_w, cellHeight=cell_h,
                pivotX=round(anchor_x / cell_w, 6),
                pivotY=round((cell_h - anchor_y) / cell_h, 6))
    return hoja, cebolla, meta


# ---------------------------------------------------------------- recorrido

def clave_anim(nombre):
    """De '03_ataque.mp4' saca 'ataque'; de '09_fase2_ataque_grieta', el resto."""
    base = os.path.splitext(os.path.basename(nombre))[0]
    base = re.sub(r"\s*\(\d+\)$", "", base)          # copias " (1)" de descarga
    base = re.sub(r"^\d+[_\-\s]*", "", base)
    base = re.sub(r"[^a-z0-9_]+", "_", base.strip().lower()).strip("_")
    # solo se colapsa el nombre cuando esta duplicado de verdad
    # ('idle01_idle' -> 'idle'). Un sufijo con significado se respeta, o el
    # jefe pierde sus seis ataques distintos al fusionarlos todos en "ataque".
    m = re.match(r"^([a-z_]+?)\d*_$", base)
    if m:
        return m.group(1)
    return base


def busca_clips(raiz, era_filtro, enemigo_filtro, anim_filtro):
    """[(era, enemigo, clave, ruta)] con lo que haya en disco."""
    encontrados = []
    if not os.path.isdir(raiz):
        return encontrados

    for era in sorted(os.listdir(raiz)):
        if era_filtro and era != era_filtro:
            continue
        base = os.path.join(raiz, era, "enemigos")
        if not os.path.isdir(base):
            continue
        for enemigo in sorted(os.listdir(base)):
            if enemigo.startswith("_") or (enemigo_filtro and enemigo != enemigo_filtro):
                continue
            vids = os.path.join(base, enemigo, "videos")
            if not os.path.isdir(vids):
                continue

            porclave = {}

            # Algunos enemigos tienen sus tomas en subcarpetas (videos/03_ataque/...)
            # con varias opciones dentro. Se mira un nivel hacia abajo y manda
            # la mas reciente. Las carpetas de descartes se saltan.
            candidatos = []
            for fn in sorted(os.listdir(vids)):
                ruta = os.path.join(vids, fn)
                if os.path.isfile(ruta) and fn.lower().endswith(VIDEO_EXT):
                    candidatos.append((clave_anim(fn), ruta))
                elif os.path.isdir(ruta) and not any(
                        x in fn.lower() for x in ("fallid", "descart", "aparte")):
                    for sub in sorted(os.listdir(ruta)):
                        if sub.lower().endswith(VIDEO_EXT):
                            # el nombre de la carpeta manda: es el de la animacion
                            candidatos.append((clave_anim(fn), os.path.join(ruta, sub)))

            for clave, ruta in candidatos:
                if not clave or (anim_filtro and clave != clave_anim(anim_filtro)):
                    continue
                if any(x in clave for x in IGNORAR):
                    continue
                if clave not in porclave or os.path.getmtime(ruta) > os.path.getmtime(porclave[clave]):
                    porclave[clave] = ruta

            for clave in sorted(porclave):
                encontrados.append((era, enemigo, clave, porclave[clave]))
    return encontrados


def procesa_enemigo(era, enemigo, clips, args):
    ficha = ROSTER.get(enemigo)
    if ficha is None:
        ficha = dict(alto=210, vuela=False, voltea=True)
        print(f"  ~ {enemigo} no esta en la tabla: uso {ficha['alto']} px y anclaje por los pies")

    alto = args.altura or ficha["alto"]
    vuela = ficha["vuela"] if args.ancla is None else (args.ancla == "vuelo")
    args.voltea = ficha.get("voltea", True) if args.voltear is None else (args.voltear == "si")
    filtro = FILTROS[args.filtro]

    salida = os.path.join(args.salida, era, enemigo)
    previews = os.path.join(args.previews, enemigo)
    os.makedirs(salida, exist_ok=True)
    os.makedirs(previews, exist_ok=True)

    # la animacion de referencia va primero: fija el factor de escala del
    # enemigo entero
    orden = sorted(clips, key=lambda c: (REF_ANIMS.index(c[0]) if c[0] in REF_ANIMS
                                        else len(REF_ANIMS), c[0]))
    factor = None
    manifest = dict(enemy=enemigo, era=era, targetHeight=alto,
                    anchor="vuelo" if vuela else "pies", scale=1.0, animations=[])

    print(f"\n{enemigo}  ({era}, {alto} px, ancla {'de vuelo' if vuela else 'por los pies'})")

    for clave, ruta in orden:
        objetivo, fps, bucle = perfil(clave)
        if args.fotogramas:
            objetivo = args.fotogramas
        if args.fps:
            fps = args.fps
        args.bucle = bucle
        args.final = 'muerte' in clave

        # un corte revisado a mano manda sobre todo lo demas
        corte = CORTES.get(enemigo, {}).get(clave)
        desde_orig, hasta_orig, fot_orig = args.desde, args.hasta, args.fotogramas
        if corte and args.desde is None and args.hasta is None:
            if isinstance(corte, dict):
                # una animacion puede salir de OTRO clip: a veces el mejor idle
                # esta en un tramo del caminar donde el bicho flota sin avanzar
                otro = corte.get("video")
                if otro:
                    for ext in VIDEO_EXT:
                        cand = os.path.join(os.path.dirname(ruta), otro + ext)
                        if os.path.exists(cand):
                            ruta = cand
                            break
                    else:
                        print(f"  ! {clave}: no encuentro el clip '{otro}', uso el suyo")
                args.tinte = corte.get("tinte")
                args.desde = float(corte["desde"])
                args.hasta = float(corte["hasta"])
                if corte.get("fotogramas"):
                    objetivo = int(corte["fotogramas"])
            else:
                args.tinte = None
                args.desde, args.hasta = float(corte[0]), float(corte[1])
                if len(corte) > 2 and corte[2]:
                    objetivo = int(corte[2])

        try:
            imgs, offs, ref, tramo = extrae_anim(ruta, args, objetivo, factor, filtro)
        except Exception as exc:                       # noqa: BLE001
            print(f"  ! {clave:22s} {exc}")
            continue

        if factor is None:
            factor = alto / max(1.0, ref)
            manifest["scale"] = round(factor, 6)
            if abs(factor - 1.0) > 1e-3:
                # la referencia se midio sin escalar: se rehace ya a su tamano
                imgs, offs, _, tramo = extrae_anim(ruta, args, objetivo, factor, filtro)
            print(f"  referencia '{clave}': {ref:.0f} px en el video -> factor {factor:.3f}")

        hoja, cebolla, meta = monta_hoja(imgs, offs, vuela)

        archivo = f"{enemigo}_{clave}.png"
        hoja.save(os.path.join(salida, archivo), optimize=True)
        cebolla.save(os.path.join(previews, f"{clave}_cebolla.png"))

        tira = cuadricula(hoja.size)
        tira.alpha_composite(hoja)
        tira.save(os.path.join(previews, f"{clave}_tira.png"))

        entrada = dict(
            name=clave, file=archivo, frames=meta["frames"],
            cols=meta["cols"], rows=meta["rows"],
            cellWidth=meta["cellWidth"], cellHeight=meta["cellHeight"],
            pivotX=meta["pivotX"], pivotY=meta["pivotY"],
            fps=fps, loop=bucle,
            clipStart=round(tramo[0], 3), clipEnd=round(tramo[1], 3))

        # el sonido se corta en el mismo tramo que la animacion
        nivel = None
        if not args.sin_audio and any(k in clave for k in AUDIO_EN):
            wav = f"{enemigo}_{clave}.wav"
            nivel = corta_audio(ruta, tramo, os.path.join(salida, wav))
            if nivel is not None and nivel >= args.audio_minimo:
                entrada["sound"] = wav
                entrada["soundLevel"] = nivel
            elif nivel is not None:
                os.remove(os.path.join(salida, wav))      # era practicamente silencio

        entrada["cutBy"] = "mano" if corte else "auto"
        entrada["sourceClip"] = os.path.basename(ruta)
        manifest["animations"].append(entrada)
        args.desde, args.hasta, args.fotogramas = desde_orig, hasta_orig, fot_orig

        sonido = ""
        if "sound" in entrada:
            sonido = f"  + audio {entrada['soundLevel']:.3f}"
        elif nivel is not None:
            sonido = f"  (audio descartado, {nivel:.3f})"

        print(f"  {clave:22s} {meta['frames']:3d} fot.  celda {meta['cellWidth']}x{meta['cellHeight']}"
              f"  hoja {meta['cols']}x{meta['rows']}  {fps} fps"
              f"  video {tramo[0]:.1f}-{tramo[1]:.1f}s{'  bucle' if bucle else ''}{sonido}")

    if not manifest["animations"]:
        return None

    manifest["animations"].sort(key=lambda a: a["name"])
    ruta_manifest = os.path.join(salida, f"{enemigo}_anim_manifest.json")
    with open(ruta_manifest, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
    return manifest


def escribe_indice(args, hechos):
    """Indice general, para que el importador de Unity haga una sola pasada."""
    if not hechos:
        return
    ruta = os.path.join(args.salida, "enemigos_manifest.json")
    previo = {}
    if os.path.exists(ruta):
        try:
            with open(ruta, "r", encoding="utf-8") as f:
                previo = {e["name"]: e for e in json.load(f).get("enemies", [])}
        except (ValueError, KeyError):
            previo = {}

    for m in hechos:
        previo[m["enemy"]] = dict(
            name=m["enemy"], era=m["era"],
            folder=f"{m['era']}/{m['enemy']}",
            manifest=f"{m['era']}/{m['enemy']}/{m['enemy']}_anim_manifest.json",
            targetHeight=m["targetHeight"], anchor=m["anchor"],
            animations=len(m["animations"]))

    with open(ruta, "w", encoding="utf-8") as f:
        json.dump({"enemies": [previo[k] for k in sorted(previo)]}, f, indent=2, ensure_ascii=False)
    return ruta


# ------------------------------------------------------------------------ cli

def parse_marca(txt):
    if not txt or txt.lower() in ("no", "0", "ninguna"):
        return None
    m = re.match(r"^([\d.]+)\s*[x*]\s*([\d.]+)$", txt.strip())
    if not m:
        raise argparse.ArgumentTypeError("la marca se escribe como 0.22x0.12")
    return float(m.group(1)), float(m.group(2))


def main():
    p = argparse.ArgumentParser(
        description="Monta las hojas de sprites de los enemigos desde sus clips de video.")
    p.add_argument("--era", help="medieval, industrial, futuro, hora_cero")
    p.add_argument("--enemigo")
    p.add_argument("--anim", help="una sola animacion, p.ej. 03_ataque o ataque")
    p.add_argument("--listar", action="store_true", help="solo dice que clips ve")

    p.add_argument("--fotogramas", type=int, help="fotogramas de la hoja (por defecto, segun el tipo)")
    p.add_argument("--fps", type=int, help="fps de la animacion en el juego")
    p.add_argument("--desde", type=float, help="segundo de entrada del clip")
    p.add_argument("--hasta", type=float, help="segundo de salida del clip")
    p.add_argument("--altura", type=int, help=f"altura del enemigo en px (Ekkar mide {EKKAR_PX})")
    p.add_argument("--ancla", choices=("pies", "vuelo"))
    p.add_argument("--filtro", choices=tuple(FILTROS), default="lanczos")

    p.add_argument("--umbral-bajo", type=int, default=25, dest="umbral_bajo",
                   help="por debajo de esto el pixel es personaje")
    p.add_argument("--umbral-alto", type=int, default=60, dest="umbral_alto",
                   help="por encima de esto el pixel es fondo")
    # Los clips salieron de Flow con la marca visible desactivada, asi que por
    # defecto no se recorta ninguna esquina. Se deja el parametro por si algun
    # clip futuro si la lleva.
    p.add_argument("--marca", type=parse_marca, default="no",
                   help="esquina inferior derecha que se ignora, p.ej. 0.22x0.12")
    p.add_argument("--voltear", choices=("si", "no"),
                   help="mirar a la izquierda; por defecto, lo que diga la tabla")
    p.add_argument("--sin-audio", action="store_true", dest="sin_audio")
    p.add_argument("--audio-minimo", type=float, default=0.004, dest="audio_minimo",
                   help="por debajo de este nivel el corte se considera silencio")

    p.add_argument("--raiz", default=PROMPTS_DIR)
    p.add_argument("--salida", default=ART_DIR)
    p.add_argument("--previews", default=PREVIEW_DIR)
    args = p.parse_args()

    comprueba_ffmpeg()
    global CORTES
    CORTES = carga_cortes()

    clips = busca_clips(args.raiz, args.era, args.enemigo, args.anim)
    if not clips:
        print("No encontre ningun clip.")
        print(f"Deja los videos en {os.path.join(args.raiz, '<era>', 'enemigos', '<enemigo>', 'videos')}")
        print("con el nombre del prompt: 01_idle.mp4, 02_caminar.mp4, 03_ataque.mp4...")
        return

    porenemigo = {}
    for era, enemigo, clave, ruta in clips:
        porenemigo.setdefault((era, enemigo), []).append((clave, ruta))

    if args.listar:
        for (era, enemigo), lista in porenemigo.items():
            ficha = ROSTER.get(enemigo, dict(alto=210, vuela=False))
            print(f"\n{enemigo}  ({era}, {ficha['alto']} px)")
            for clave, ruta in sorted(lista):
                w, h, fps = info_video(ruta)
                print(f"  {clave:22s} {w}x{h} @ {fps:.0f} fps   {os.path.basename(ruta)}")
        return

    os.makedirs(args.salida, exist_ok=True)
    os.makedirs(args.previews, exist_ok=True)

    hechos = []
    for (era, enemigo), lista in porenemigo.items():
        m = procesa_enemigo(era, enemigo, lista, args)
        if m:
            hechos.append(m)

    indice = escribe_indice(args, hechos)
    total = sum(len(m["animations"]) for m in hechos)
    print(f"\n{len(hechos)} enemigos, {total} animaciones")
    print(f"hojas     -> {args.salida}")
    print(f"revision  -> {args.previews}")
    if indice:
        print(f"indice    -> {indice}")


if __name__ == "__main__":
    main()
