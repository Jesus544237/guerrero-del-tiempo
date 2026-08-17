# -*- coding: utf-8 -*-
"""
Monta la cinematica de entrada con el arte que ya hay, y la deja lista para
el juego en Assets/StreamingAssets/intro.mp4.

Aviso de lo que esto es y de lo que no:

  NO son los tres clips de video generados con Veo/Gemini que describe
  Docs/cinematica_lore_y_objetos.md. Eso hay que generarlo fuera, y los
  prompts siguen valiendo tal cual. Esto es una cinematica hecha con el key
  art y las animaciones del propio juego: se puede meter hoy, cuenta la
  misma historia y respeta los mismos tres planos y los mismos rotulos.
  Cuando tengas los clips buenos, se reemplaza el intro.mp4 y ya.

Los tres planos, igual que en el documento:

  1 (0-5 s)    El Gran Reloj estalla y salen los tres fragmentos.
  2 (5-10 s)   Las tres eras deshaciendose, con fogonazo entre una y otra.
  3 (10-15 s)  Ekkar desenvaina, la onda cian congela el mundo y el titulo.

La musica y los golpes de sonido se sintetizan aqui, como el resto de la
banda sonora del juego. La voz del narrador no: si la quieres, grabala y
montala encima, que los rotulos ya dejan el hueco.

Uso:
    python Tools/monta_intro.py
    python Tools/monta_intro.py --salida C:\\ruta\\otra_cosa.mp4 --sin-copiar
"""

import argparse
import math
import os
import shutil
import struct
import subprocess
import sys
import tempfile
import wave

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ANCHO, ALTO, FPS = 1920, 1080, 30
DURACION = 15.0
SR = 44100

PROY = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REFS = os.path.join(PROY, "Docs", "Referencias")
SENA = r"C:\Users\usuario\Documents\SENA\programacion de video juegos\ekkar"
ANIM = os.path.join(PROY, "Assets", "_Game", "Art", "Characters", "Ekkar", "Anim")
FUENTE = os.path.join(PROY, "Assets", "_Game", "Art", "Fonts", "PerfectDOSVGA437.ttf")

CIAN = (34, 212, 237)
ORO = (250, 191, 36)

# Los rotulos que pide el documento. En pantalla, no en boca del narrador.
ROTULOS = [
    (0.9, 4.6, "El Gran Reloj se rompió."),
    (5.4, 9.6, "Tres eras. Tres fragmentos."),
]
TITULO = (13.9, 15.0, "GUERRERO DEL TIEMPO")

# ---------------------------------------------------------------- las fuentes


def carga(ruta, escala_previa=2):
    """Abre una imagen y la agranda a bloques, para que el pixel siga siendo pixel."""
    im = Image.open(ruta).convert("RGB")
    if escala_previa > 1:
        im = im.resize((im.width * escala_previa, im.height * escala_previa), Image.NEAREST)
    return im


def recorta(im, t01, zoom_ini, zoom_fin, hacia=(0.5, 0.5), desde=(0.5, 0.5)):
    """
    Un travelling: recorta una ventana 16:9 que se abre o se cierra y la lleva
    de un punto a otro de la imagen. Es lo que da sensacion de camara.
    """
    k = t01 * t01 * (3 - 2 * t01)          # suaviza el arranque y el final
    zoom = zoom_ini + (zoom_fin - zoom_ini) * k
    cx = desde[0] + (hacia[0] - desde[0]) * k
    cy = desde[1] + (hacia[1] - desde[1]) * k

    ancho = im.width / zoom
    alto = ancho * ALTO / ANCHO
    if alto > im.height:
        alto = im.height
        ancho = alto * ANCHO / ALTO

    x = min(max(cx * im.width - ancho / 2, 0), im.width - ancho)
    y = min(max(cy * im.height - alto / 2, 0), im.height - alto)
    caja = (int(x), int(y), int(x + ancho), int(y + alto))
    return im.resize((ANCHO, ALTO), Image.LANCZOS, box=caja)


def celdas_ekkar():
    """Los 24 fotogramas de desenvainar, ya troceados y agrandados."""
    import json

    man = json.load(open(os.path.join(ANIM, "ekkar_anim_manifest.json"), encoding="utf-8"))
    a = next(x for x in man["animations"] if x["name"] == "desenvainar")
    hoja = Image.open(os.path.join(ANIM, a["file"])).convert("RGBA")
    cw, ch, cols = a["cellWidth"], a["cellHeight"], a["cols"]

    fuera = []
    for i in range(a["frames"]):
        col, fila = i % cols, i // cols
        cel = hoja.crop((col * cw, fila * ch, col * cw + cw, fila * ch + ch))
        fuera.append(cel.resize((cw * 4, ch * 4), Image.NEAREST))
    return fuera


# ------------------------------------------------------------------ efectos


def vineta():
    x = np.linspace(-1, 1, ANCHO)[None, :]
    y = np.linspace(-1, 1, ALTO)[:, None]
    r = np.sqrt(x * x + y * y) / math.sqrt(2)
    return np.clip(1.15 - 0.55 * r ** 2.2, 0, 1)[:, :, None]


VINETA = vineta()


def aberracion(a, px):
    """Separa un pelin el rojo y el azul: el toque de lente barata que pide el estilo."""
    if px <= 0:
        return a
    fuera = a.copy()
    fuera[:, px:, 0] = a[:, :-px, 0]
    fuera[:, :-px, 2] = a[:, px:, 2]
    return fuera


def acabado(im, aberra=1, grano=0.02, rng=None):
    a = np.asarray(im, dtype=np.float32)
    a = aberracion(a, aberra)
    a *= VINETA
    if grano > 0:
        # el grano se genera pequeno y se estira: sale mas de pelicula y
        # cuesta la decima parte que hacerlo a tamano completo
        chico = rng.normal(0, grano * 255, (ALTO // 4, ANCHO // 4)).astype(np.float32)
        grande = np.asarray(Image.fromarray(chico).resize((ANCHO, ALTO), Image.BILINEAR))
        a += grande[:, :, None]
    return np.clip(a, 0, 255).astype(np.uint8)


def mezcla_color(a, color, fuerza):
    if fuerza <= 0:
        return a
    c = np.array(color, dtype=np.float32)
    return (a.astype(np.float32) * (1 - fuerza) + c * fuerza).astype(np.uint8)


# ------------------------------------------------------------------- rotulos


def fuente(px):
    try:
        return ImageFont.truetype(FUENTE, px)
    except OSError:
        return ImageFont.truetype("arialbd.ttf", px)


F_ROTULO = fuente(48)
F_TITULO = fuente(112)


def escribe(im, texto, f, y, alfa, color=(255, 255, 255)):
    """Escribe centrado y con borde negro; devuelve la imagen ya compuesta."""
    if alfa <= 0.01:
        return im
    capa = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(capa)
    caja = d.textbbox((0, 0), texto, font=f)
    x = (ANCHO - (caja[2] - caja[0])) // 2 - caja[0]
    a = int(255 * alfa)
    # sobre pixel art claro, el texto blanco a secas se pierde
    for dx, dy in ((-3, 0), (3, 0), (0, -3), (0, 3), (-2, -2), (2, -2), (-2, 2), (2, 2)):
        d.text((x + dx, y + dy), texto, font=f, fill=(0, 0, 0, a))
    d.text((x, y), texto, font=f, fill=color + (a,))
    return Image.alpha_composite(im.convert("RGBA"), capa).convert("RGB")


def desvanece(t, entra, sale, rampa=0.45):
    """1 dentro del tramo, con entrada y salida suaves."""
    if t < entra - rampa or t > sale + rampa:
        return 0.0
    if t < entra:
        return (t - (entra - rampa)) / rampa
    if t > sale:
        return max(0.0, 1 - (t - sale) / rampa)
    return 1.0


# -------------------------------------------------------------- los 3 planos


class Montaje:
    def __init__(self):
        self.prologo = carga(os.path.join(SENA, "scene_prologue.png"), 2)
        self.eras = [carga(os.path.join(REFS, f"era_{i}_{n}.png"), 3)
                     for i, n in ((1, "edad_media"), (2, "industrial"), (3, "futuro"))]
        self.vacio = carga(os.path.join(REFS, "era_4_hora_cero.png"), 3)
        self.ekkar = celdas_ekkar()
        self.rng = np.random.default_rng(20260816)

    # ---- plano 1: el reloj estalla
    def plano1(self, t):
        u = t / 5.0
        im = recorta(self.prologo, u, 1.55, 1.12, hacia=(0.5, 0.42), desde=(0.5, 0.30))
        a = acabado(im, aberra=2, rng=self.rng)

        # el estallido: un fogonazo corto que se apaga deprisa
        if 0.55 <= t <= 1.5:
            k = max(0.0, 1 - (t - 0.55) / 0.95)
            a = mezcla_color(a, (255, 255, 255), k ** 2 * 0.85)
        return a

    # ---- plano 2: las tres eras
    def plano2(self, t):
        u = t - 5.0
        cual = min(int(u / 1.667), 2)
        dentro = (u - cual * 1.667) / 1.667
        im = recorta(self.eras[cual], dentro, 1.30, 1.02,
                     hacia=(0.5, 0.45), desde=(0.42 + 0.08 * cual, 0.45))
        a = acabado(im, aberra=2, rng=self.rng)

        # fogonazo blanco en cada corte, que es lo que cose los tres sitios
        if dentro < 0.22:
            a = mezcla_color(a, (255, 255, 255), (1 - dentro / 0.22) ** 2 * 0.9)
        return a

    # ---- plano 3: Ekkar, la onda y el titulo
    def plano3(self, t):
        u = t - 10.0
        fondo = recorta(self.vacio, min(u / 5.0, 1.0), 1.22, 1.04,
                        hacia=(0.5, 0.42), desde=(0.5, 0.5))

        # el desenvaine ocupa de 0,5 a 2,5 s; antes y despues se queda quieto
        if u < 0.5:
            cel = self.ekkar[0]
        elif u < 2.5:
            cel = self.ekkar[min(int((u - 0.5) * 12), len(self.ekkar) - 1)]
        else:
            cel = self.ekkar[-1]

        fondo = fondo.convert("RGBA")
        fondo.alpha_composite(cel, ((ANCHO - cel.width) // 2, ALTO - cel.height - 60))
        a = acabado(fondo.convert("RGB"), aberra=2, rng=self.rng)

        # la onda cian sale de la espada y barre la pantalla
        if 2.3 <= u <= 3.4:
            k = (u - 2.3) / 1.1
            radio = k * ANCHO * 1.15
            x = np.arange(ANCHO)[None, :] - ANCHO / 2
            y = np.arange(ALTO)[:, None] - ALTO * 0.42
            d = np.sqrt(x * x + y * y)
            banda = np.exp(-((d - radio) / 90.0) ** 2)[:, :, None]
            a = np.clip(a.astype(np.float32) + banda * np.array(CIAN) * 1.5,
                        0, 255).astype(np.uint8)
            # y por dentro de la onda el mundo se queda helado
            a = np.where((d < radio)[:, :, None], mezcla_color(a, CIAN, 0.16), a)
        elif u > 3.4:
            a = mezcla_color(a, CIAN, 0.16)

        # el mundo helado se apaga y Ekkar queda en silueta: es el fondo sobre
        # el que se lee el titulo, que de otro modo caia sobre su cara
        if u > 3.5:
            a = mezcla_color(a, (0, 0, 0), min(0.82, (u - 3.5) / 1.1 * 0.82))
        # y el ultimo parpadeo a negro del todo
        if u > 4.85:
            a = mezcla_color(a, (0, 0, 0), min(1.0, (u - 4.85) / 0.15))
        return a

    def fotograma(self, t):
        if t < 5.0:
            a = self.plano1(t)
        elif t < 10.0:
            a = self.plano2(t)
        else:
            a = self.plano3(t)

        im = Image.fromarray(a)
        for entra, sale, texto in ROTULOS:
            im = escribe(im, texto, F_ROTULO, ALTO - 150, desvanece(t, entra, sale))
        im = escribe(im, TITULO[2], F_TITULO, ALTO // 2 - 70,
                     desvanece(t, TITULO[0], TITULO[1], 0.7), ORO)

        # entrada desde negro
        if t < 0.6:
            im = Image.fromarray(mezcla_color(np.asarray(im), (0, 0, 0), 1 - t / 0.6))
        return im


# -------------------------------------------------------------------- sonido


def sonido():
    n = int(DURACION * SR)
    out = np.zeros(n)
    rng = np.random.default_rng(4093)

    def en(seg, muestras):
        i = int(seg * SR)
        j = min(i + len(muestras), n)
        if j > i:
            out[i:j] += muestras[: j - i]

    def tic(f, largo=0.05, vol=0.5, caida=70.0):
        tl = np.arange(int(largo * SR)) / SR
        return np.sin(2 * math.pi * f * tl) * np.exp(-caida * tl) * vol

    # --- plano 1: el tictac que se acelera hasta romperse
    reloj, paso, cual = 0.0, 0.42, 0
    while reloj < 1.05:
        en(reloj, tic(2200 if cual % 2 == 0 else 1650, vol=0.45))
        paso *= 0.74
        reloj += paso
        cual += 1

    # el estallido: cristal (ruido agudo) y un golpe grave debajo
    tl = np.arange(int(1.8 * SR)) / SR
    cristal = rng.normal(0, 1, len(tl)) * np.exp(-4.5 * tl) * 0.5
    cristal *= 0.5 + 0.5 * np.sin(2 * math.pi * 5200 * tl)
    en(0.55, cristal)
    en(0.55, np.sin(2 * math.pi * 41 * tl) * np.exp(-2.2 * tl) * 0.8)

    # zumbido grave que se queda flotando hasta el final del plano
    tramo = np.arange(int(4.5 * SR)) / SR
    drone = (np.sin(2 * math.pi * 55 * tramo) * 0.16
             + np.sin(2 * math.pi * 82.41 * tramo) * 0.10)
    drone *= np.clip((tramo - 0.1) / 0.8, 0, 1) * np.clip((4.5 - tramo) / 1.2, 0, 1)
    en(0.5, drone)

    # --- plano 2: un golpe por era: campana, maquinaria, pitido digital
    en(5.02, tic(196, largo=2.2, vol=0.5, caida=2.4) + tic(392, largo=2.2, vol=0.2, caida=3.0))
    tl = np.arange(int(1.2 * SR)) / SR
    en(6.69, (np.sin(2 * math.pi * 62 * tl) + rng.normal(0, 0.35, len(tl)))
       * np.exp(-5.5 * tl) * 0.45)
    en(8.35, np.sign(np.sin(2 * math.pi * 880 * tl)) * np.exp(-9.0 * tl) * 0.22)

    # --- plano 3: la espada, la descarga y el silencio
    tl = np.arange(int(0.9 * SR)) / SR
    en(11.1, np.sin(2 * math.pi * 3100 * tl * (1 + 0.4 * tl)) * np.exp(-6.0 * tl) * 0.30
       + rng.normal(0, 1, len(tl)) * np.exp(-22.0 * tl) * 0.18)

    tl = np.arange(int(2.4 * SR)) / SR
    en(12.3, (np.sin(2 * math.pi * 130.81 * tl) * 0.5
              + np.sin(2 * math.pi * 196 * tl) * 0.3) * np.exp(-1.5 * tl) * 0.55)

    # la nota que sostiene el titulo
    tl = np.arange(int(2.1 * SR)) / SR
    en(12.9, (np.sin(2 * math.pi * 65.41 * tl) * 0.5 + np.sin(2 * math.pi * 98 * tl) * 0.22)
       * np.clip(tl / 0.4, 0, 1) * np.exp(-0.8 * tl) * 0.6)

    # cierre limpio y sin pasarse de volumen
    out[: int(0.05 * SR)] *= np.linspace(0, 1, int(0.05 * SR))
    out[-int(0.3 * SR):] *= np.linspace(1, 0, int(0.3 * SR))
    pico = np.max(np.abs(out))
    if pico > 0:
        out *= 0.82 / pico
    return out


def escribe_wav(ruta, data):
    pcm = (np.clip(data, -1, 1) * 32767).astype(np.int16)
    with wave.open(ruta, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(struct.pack("<%dh" % len(pcm), *pcm))


# --------------------------------------------------------------------- main


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--salida", default=None)
    p.add_argument("--sin-copiar", action="store_true",
                   help="no lo deja en Assets/StreamingAssets")
    args = p.parse_args()

    if shutil.which("ffmpeg") is None:
        sys.exit("Hace falta ffmpeg en el PATH.")

    tmp = tempfile.mkdtemp(prefix="intro_")
    wav = os.path.join(tmp, "pista.wav")
    salida = args.salida or os.path.join(PROY, "Tools", "out", "intro.mp4")
    os.makedirs(os.path.dirname(salida), exist_ok=True)

    print("Sintetizando la pista de sonido...")
    escribe_wav(wav, sonido())

    print("Montando los fotogramas...")
    montaje = Montaje()
    total = int(DURACION * FPS)

    cmd = [
        "ffmpeg", "-y", "-loglevel", "error",
        "-f", "rawvideo", "-pix_fmt", "rgb24",
        "-s", f"{ANCHO}x{ALTO}", "-r", str(FPS), "-i", "-",
        "-i", wav,
        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20", "-preset", "medium",
        "-c:a", "aac", "-b:a", "192k", "-shortest", salida,
    ]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for i in range(total):
        proc.stdin.write(montaje.fotograma(i / FPS).convert("RGB").tobytes())
        if (i + 1) % 60 == 0:
            print(f"  {i + 1}/{total} fotogramas")
    proc.stdin.close()
    if proc.wait() != 0:
        sys.exit("ffmpeg ha fallado.")

    tam = os.path.getsize(salida) / 1024 / 1024
    print(f"\n  {salida}  ({tam:.1f} MB, {DURACION:.0f}s, {ANCHO}x{ALTO}, {FPS} fps)")

    if not args.sin_copiar:
        destino_dir = os.path.join(PROY, "Assets", "StreamingAssets")
        os.makedirs(destino_dir, exist_ok=True)
        destino = os.path.join(destino_dir, "intro.mp4")
        shutil.copy2(salida, destino)
        print(f"  copiada en {destino}")

    shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    main()
