# -*- coding: utf-8 -*-
"""
Monta la cinematica final: le pone delante el Gran Reloj rompiendose, y encima
los rotulos con la letra, los colores y la animacion de aparecer del juego.

Lo que hace, por partes:

  1. Fabrica 6 segundos de apertura que hoy no existen: la torre del reloj
     entera, primero de cerca y girando tranquila, luego las grietas, el
     estallido, y la camara abriendose hasta dejar el key art completo — que es
     exactamente la ilustracion de scene_prologue.png. La explosion se saca de
     la propia ilustracion: se separan los cristales, los engranajes y los
     rayos, se rellena el cielo por detras, y se les hace salir del centro.

  2. Pega detras el video de 27 s tal cual, con grano, vineta y una aberracion
     cromatica leve — el bloque de estilo del dossier, ni mas ni menos.

  3. Pone cuatro carteles clavados a los del juego: la franja oscura, las dos
     reglas doradas, la tipografia PerfectDOSVGA437 y la misma forma de
     aparecer que BossBanner (entra ancho y aplastado, se asienta, y destella
     en oro). Los tiempos van cuadrados con los cortes reales del video.

  4. Deja el audio original donde estaba y le sintetiza el sonido a los 6 s
     nuevos. Aparte saca una pista completa de 33 s por si hay que silenciar el
     original para meter la voz nueva.

Uso:
    python Tools/monta_cinematica_jesus.py
    python Tools/monta_cinematica_jesus.py --solo-apertura   # para revisar
"""

import argparse
import math
import os
import struct
import subprocess
import sys
import wave

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ANCHO, ALTO, FPS = 1920, 1080, 30
SR = 44100

PROY = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FUENTE = os.path.join(PROY, "Assets", "_Game", "Art", "Fonts", "PerfectDOSVGA437.ttf")
ARTE = os.environ.get("EKKAR_ARTE") or os.path.join(
    os.path.expanduser("~"), "Documents", "SENA", "programacion de video juegos", "ekkar")
KEYART = os.path.join(ARTE, "scene_prologue.png")
# El video con la narracion grabada. Se pasa por EKKAR_NARRACION.
ENTRADA = os.environ.get("EKKAR_NARRACION") or os.path.join(
    os.path.expanduser("~"), "Videos", "narracion.mp4")
SALIDA = os.path.join(PROY, "Tools", "out", "guerrero_del_tiempo_cinematica.mp4")
PISTA = os.path.join(PROY, "Tools", "out", "pista_completa_33s.wav")

APERTURA = 6.0                    # lo que dura el trozo nuevo
DURACION_ORIGINAL = 27.0
TOTAL = APERTURA + DURACION_ORIGINAL

# Los colores del juego, sacados de BossBanner.cs tal cual
ORO = (250, 192, 36)
CIAN = (34, 211, 237)
HUESO = (226, 231, 235)
FRANJA = (10, 5, 20)

# Puntos de la ilustracion, medidos sobre el png de 1024x1024
NUCLEO = (478, 265)               # el corazon blanco del estallido
RELOJES = (378, 521)              # entre las dos esferas
VENTANA_FINAL = (0, 20, 1024, 596)      # el encuadre 16:9 del final
VENTANA_RELOJ = (378 - 260, 521 - 146, 378 + 260, 521 + 146)


# ---------------------------------------------------------------- los carteles
#
# Tiempos cuadrados con los cortes reales del video (medidos con signalstats):
#   15,0-18,4 Edad Media | 18,9-21,4 Industrial | 22,0-23,8 Futuro
#   24,2-28,3 Ekkar      | 28,9-32,1 su cara
#
# Ojo con los acentos: PerfectDOSVGA437 es una fuente de CP437, y coloca cada
# glifo en el byte que le tocaba en DOS, no en su punto Unicode. Escribir "ó"
# saca un "≤". Se arregla traduciendo a CP437 (ver cp437 mas abajo), que
# funciona para á í ó ú ñ Ñ ¿ ¡ É — pero NO para la "é" minuscula, que cae en
# 0x82, dentro del hueco de caracteres de control. Por eso aqui no hay ninguna.
#
# El cuarto numero baja el cartel: en el juego va centrado, pero los dos
# ultimos planos son Ekkar de cuerpo entero y su cara en primer plano, y
# centrado le tapaba justo los ojos.
CARTELES = [
    (4.60, "EL GRAN RELOJ", "Se rompió, y con su caída cayó el orden de las eras", 0),
    (11.60, "TRES FRAGMENTOS", "Tres eras empezaron a deshacerse", 0),
    (24.90, "EKKAR", "El último Guerrero del Tiempo", 250),
    (29.60, "GUERRERO DEL TIEMPO", "El destino de todas las eras está en tus manos", 268),
]
DURA_CARTEL = 3.2                 # lo mismo que BossBanner.Presenta


def cp437(texto):
    """Pasa el texto al hueco donde la fuente guarda de verdad cada glifo."""
    fuera = []
    for ch in texto:
        try:
            b = ch.encode("cp437")
        except UnicodeEncodeError:
            fuera.append(ch)
            continue
        fuera.append(chr(b[0]) if len(b) == 1 and b[0] >= 0xA0 else ch)
    return "".join(fuera)


def fuente(px):
    try:
        return ImageFont.truetype(FUENTE, px)
    except OSError:
        return ImageFont.truetype("arialbd.ttf", px)


F_NOMBRE = fuente(84)
F_SUB = fuente(34)


def cartel(im, nombre, subtitulo, t, baja=0):
    """
    Un cartel del juego. Copia lo que hace BossBanner: la alfa sube a 3,2 por
    segundo y la escala entra en (1,1 x 0,75) y se asienta en (1,1) a 7 por
    segundo. Los ultimos 0,7 s se va.
    """
    if t < 0 or t > DURA_CARTEL:
        return im, 0.0

    alfa = min(1.0, t * 3.2)
    queda = DURA_CARTEL - t
    if queda < 0.7:
        alfa = min(alfa, queda / 0.7)
    if alfa <= 0.01:
        return im, 0.0

    # el asentamiento de la escala: Lerp(dt*7) es una caida exponencial
    k = math.exp(-7.0 * t)
    ex, ey = 1.0 + 0.10 * k, 1.0 - 0.25 * k

    # se dibuja el bloque a tamaño normal y luego se deforma entero
    bloque = Image.new("RGBA", (1500, 320), (0, 0, 0, 0))
    d = ImageDraw.Draw(bloque)
    cy = 160
    d.rectangle([0, cy - 115, 1500, cy + 115], fill=FRANJA + (199,))
    for dy in (-96, 96):
        d.rectangle([370, cy + dy - 1, 1130, cy + dy + 1], fill=ORO + (217,))

    for texto, f, color, dy in ((nombre, F_NOMBRE, ORO, -26), (subtitulo, F_SUB, HUESO, 46)):
        texto = cp437(texto)
        caja = d.textbbox((0, 0), texto, font=f)
        x = (1500 - (caja[2] - caja[0])) // 2 - caja[0]
        y = cy + dy - (caja[3] - caja[1]) // 2 - caja[1]
        d.text((x, y), texto, font=f, fill=color + (255,))

    ancho = max(1, int(1500 * ex))
    alto = max(1, int(320 * ey))
    bloque = bloque.resize((ancho, alto), Image.BICUBIC)
    if alfa < 1.0:
        bloque.putalpha(bloque.getchannel("A").point(lambda v: int(v * alfa)))

    capa = Image.new("RGBA", (ANCHO, ALTO), (0, 0, 0, 0))
    capa.alpha_composite(bloque, ((ANCHO - ancho) // 2, (ALTO - alto) // 2 + baja))
    fuera = Image.alpha_composite(im.convert("RGBA"), capa).convert("RGB")

    # El destello de oro con el que entra. En el juego es 0,35 a pantalla
    # completa; sobre video eso lava la imagen entera, asi que va a 0,16 y
    # dura menos: se nota el golpe y no se pierde el arte de debajo.
    destello = max(0.0, 0.16 * (1.0 - t / 0.30)) if t < 0.30 else 0.0
    return fuera, destello


# -------------------------------------------------------------------- efectos

def _vineta():
    x = np.linspace(-1, 1, ANCHO)[None, :]
    y = np.linspace(-1, 1, ALTO)[:, None]
    r = np.sqrt(x * x + y * y) / math.sqrt(2)
    return np.clip(1.12 - 0.42 * r ** 2.2, 0, 1)[:, :, None].astype(np.float32)


VINETA = _vineta()


def acabado(a, rng, grano=0.014, aberra=2):
    """Grano sutil, vineta y aberracion cromatica leve. Con mano ligera: el arte
    de debajo es bueno y no hay que taparlo."""
    if aberra > 0:
        b = a.copy()
        b[:, aberra:, 0] = a[:, :-aberra, 0]
        b[:, :-aberra, 2] = a[:, aberra:, 2]
        a = b
    f = a.astype(np.float32) * VINETA
    chico = rng.normal(0, grano * 255, (ALTO // 4, ANCHO // 4)).astype(np.float32)
    f += np.asarray(Image.fromarray(chico).resize((ANCHO, ALTO), Image.BILINEAR))[:, :, None]
    return np.clip(f, 0, 255).astype(np.uint8)


def tinta(a, color, fuerza):
    if fuerza <= 0:
        return a
    return (a.astype(np.float32) * (1 - fuerza)
            + np.array(color, dtype=np.float32) * fuerza).astype(np.uint8)


# ------------------------------------------------------- la apertura del reloj

class Apertura:
    """Los 6 segundos nuevos, sacados de la propia ilustracion."""

    def __init__(self):
        base = Image.open(KEYART).convert("RGB")
        self.a = np.asarray(base).astype(np.float32)
        self.mascara = self._mascara_estallido()
        self.plato = self._cielo_sin_estallido()

        # la capa que sale despedida, con su alfa
        capa = np.dstack([np.asarray(base).astype(np.uint8),
                          (self.mascara * 255).astype(np.uint8)])
        self.estallido = Image.fromarray(capa, "RGBA")
        self.limpio = Image.fromarray(self.plato.astype(np.uint8), "RGB")
        self.rng = np.random.default_rng(4093)
        self.grietas = self._grietas()

    def _mascara_estallido(self):
        R, G, B = self.a[:, :, 0], self.a[:, :, 1], self.a[:, :, 2]
        yy, xx = np.mgrid[0:1024, 0:1024]
        dist = np.sqrt((xx - NUCLEO[0]) ** 2 + (yy - NUCLEO[1]) ** 2)

        cian = (B > 150) & (B > R + 45) & (G > R + 20) & (yy < 720)
        oro = (R > 135) & (G > 95) & (B < R - 45) & (dist < 430) & (yy < 600)
        blanco = (R > 215) & (G > 215) & (B > 215) & (dist < 430)
        return (cian | oro | blanco).astype(np.float32)

    def _cielo_sin_estallido(self):
        """
        Rellena lo que se ha quitado con el color que tiene el cielo en esa
        misma fila. El cielo es un degradado morado bastante plano, asi que la
        mediana por filas lo reconstruye sin que se note.
        """
        plato = self.a.copy()
        libre = self.mascara < 0.5
        for y in range(1024):
            fila = libre[y]
            if fila.sum() < 30:
                continue
            med = np.median(self.a[y][fila], axis=0)
            plato[y][~fila] = med
        # un desenfoque corto para que no queden bandas
        suave = np.asarray(Image.fromarray(plato.astype(np.uint8))
                           .filter(__import__("PIL.ImageFilter", fromlist=["x"]).GaussianBlur(6)))
        m = self.mascara[:, :, None]
        return plato * (1 - m) + suave * m

    def _piedra(self):
        """Donde hay torre y no cielo: las grietas solo valen sobre la piedra."""
        lum = self.a.mean(axis=2)
        m = np.zeros((1024, 1024), bool)
        m[400:760, 240:530] = True
        return m & (lum < 105)

    def _grietas(self):
        """
        Las grietas que recorren la torre antes de reventar. Se quedan sobre la
        piedra a proposito: subiendo al cielo no parecian roturas, parecian
        rayos, y rayos ya tiene la ilustracion de sobra.
        """
        rng = np.random.default_rng(7)
        piedra = self._piedra()
        ramas = []
        for _ in range(11):
            ang = rng.uniform(0, 2 * math.pi)
            x, y = RELOJES[0] + rng.uniform(-55, 55), RELOJES[1] + rng.uniform(-45, 45)
            pts = [(x, y)]
            for _ in range(int(rng.integers(6, 11))):
                ang += rng.uniform(-0.7, 0.7)
                largo = rng.uniform(7, 14)
                nx, ny = x + math.cos(ang) * largo, y + math.sin(ang) * largo
                if not (0 <= int(ny) < 1024 and 0 <= int(nx) < 1024 and piedra[int(ny), int(nx)]):
                    break
                x, y = nx, ny
                pts.append((x, y))
            if len(pts) > 2:
                ramas.append(pts)
        return ramas

    # ---- la camara

    def ventana(self, k):
        """De cerca en las esferas del reloj a la ilustracion entera."""
        s = k * k * (3 - 2 * k)
        a, b = VENTANA_RELOJ, VENTANA_FINAL
        return tuple(a[i] + (b[i] - a[i]) * s for i in range(4))

    def fotograma(self, t):
        # --- reparto del tiempo
        if t < 2.6:                       # el reloj entero, empuje muy lento
            k = 0.06 * (t / 2.6)
            grieta = 0.0
        elif t < 3.4:                     # las grietas
            u = (t - 2.6) / 0.8
            k = 0.06 + 0.10 * u
            grieta = u
        elif t < 4.8:                     # revienta y la camara se abre
            u = (t - 3.4) / 1.4
            k = 0.16 + 0.84 * (u ** 0.55)
            grieta = 1.0
        else:                             # se queda en el key art
            k = 1.0 + 0.02 * ((t - 4.8) / 1.2)
            grieta = 1.0

        # --- cuanto del estallido se ve ya
        if t < 3.4:
            crece, visible = 0.0, 0.0
        else:
            u = min(1.0, (t - 3.4) / 1.15)
            crece = 0.12 + 0.88 * (1 - (1 - u) ** 2)
            visible = min(1.0, u * 2.2)

        lienzo = self.limpio.copy()
        if visible > 0:
            lado = max(2, int(1024 * crece))
            capa = self.estallido.resize((lado, lado), Image.LANCZOS)
            if visible < 1.0:
                capa.putalpha(capa.getchannel("A").point(lambda v: int(v * visible)))
            # se escala respecto al nucleo, para que salga de ahi
            ox = int(NUCLEO[0] - NUCLEO[0] * crece)
            oy = int(NUCLEO[1] - NUCLEO[1] * crece)
            lienzo = lienzo.convert("RGBA")
            lienzo.alpha_composite(capa, (ox, oy))
            lienzo = lienzo.convert("RGB")

        if grieta > 0:
            d = ImageDraw.Draw(lienzo)
            cuantas = max(1, int(len(self.grietas) * min(1.0, grieta * 1.3)))
            for pts in self.grietas[:cuantas]:
                hasta = max(2, int(len(pts) * min(1.0, grieta * 1.6)))
                trazo = [tuple(p) for p in pts[:hasta]]
                d.line(trazo, fill=(70, 190, 225), width=3)
                d.line(trazo, fill=(190, 250, 255), width=1)

        # --- encuadre, con temblor mientras se agrieta
        x0, y0, x1, y1 = self.ventana(k)
        if 2.6 < t < 4.3:
            f = 6.0 * (1 if t < 3.4 else max(0.0, 1 - (t - 3.4) / 0.9))
            x0 += self.rng.normal(0, f); x1 += self.rng.normal(0, f)
            y0 += self.rng.normal(0, f); y1 += self.rng.normal(0, f)

        im = lienzo.resize((ANCHO, ALTO), Image.LANCZOS, box=(x0, y0, x1, y1))
        a = acabado(np.asarray(im), self.rng, grano=0.016)

        # --- el fogonazo del estallido
        if 3.30 <= t < 4.10:
            f = max(0.0, 1 - (t - 3.30) / 0.80)
            a = tinta(a, (255, 255, 255), f ** 1.6 * 0.92)
        if t < 0.5:
            a = tinta(a, (0, 0, 0), 1 - t / 0.5)
        return a


# --------------------------------------------------------------------- sonido

def sonido_apertura():
    """Los 6 s nuevos: el tictac, las grietas, el estallido y el zumbido."""
    n = int(APERTURA * SR)
    out = np.zeros(n)
    rng = np.random.default_rng(4093)

    def en(seg, x):
        i = int(seg * SR); j = min(i + len(x), n)
        if j > i:
            out[i:j] += x[: j - i]

    def tl(seg):
        return np.arange(int(seg * SR)) / SR

    # tictac tranquilo hasta que empiezan las grietas
    t = 0.0
    while t < 2.7:
        x = tl(0.06)
        en(t, np.sin(2 * math.pi * (2300 if int(t / 0.6) % 2 == 0 else 1720) * x)
           * np.exp(-75 * x) * 0.30)
        t += 0.6

    # el zumbido que crece mientras se agrieta
    x = tl(0.9)
    en(2.55, (np.sin(2 * math.pi * 48 * x) * 0.5 + np.sin(2 * math.pi * 73 * x) * 0.3)
       * np.clip(x / 0.6, 0, 1) * 0.55)
    # crujidos
    for s in (2.62, 2.88, 3.05, 3.22):
        x = tl(0.25)
        en(s, rng.normal(0, 1, len(x)) * np.exp(-16 * x) * 0.16)

    # el estallido: cristal arriba y un golpe hondo debajo
    x = tl(2.4)
    cristal = rng.normal(0, 1, len(x)) * np.exp(-3.8 * x) * 0.55
    cristal *= 0.45 + 0.55 * np.sin(2 * math.pi * 5400 * x)
    en(3.30, cristal)
    en(3.30, np.sin(2 * math.pi * 38 * x) * np.exp(-1.9 * x) * 0.85)
    en(3.30, np.sin(2 * math.pi * 76 * x) * np.exp(-3.0 * x) * 0.30)

    # el zumbido grave que queda flotando
    x = tl(2.6)
    en(3.4, (np.sin(2 * math.pi * 55 * x) * 0.18 + np.sin(2 * math.pi * 82.41 * x) * 0.11)
       * np.clip(x / 0.5, 0, 1) * np.clip((2.6 - x) / 1.0, 0, 1))

    out[: int(0.04 * SR)] *= np.linspace(0, 1, int(0.04 * SR))
    pico = np.max(np.abs(out))
    if pico > 0:
        out *= 0.80 / pico
    return out


def pista_completa():
    """
    Una pista de 33 s para todo el montaje, cuadrada con los cortes reales.

    Existe por un problema practico: el audio del video trae la narracion
    metida en la mezcla, y no hay forma de sacarla sin llevarse por delante la
    musica. Si hay que silenciar el original para poner la voz nueva, esto
    ocupa su sitio.
    """
    n = int(TOTAL * SR)
    out = np.zeros(n)
    rng = np.random.default_rng(20260816)

    ap = sonido_apertura()
    out[: len(ap)] += ap * 0.9

    def en(seg, x, vol=1.0):
        i = int(seg * SR); j = min(i + len(x), n)
        if j > i:
            out[i:j] += x[: j - i] * vol

    def tl(seg):
        return np.arange(int(seg * SR)) / SR

    def golpe(largo, f, caida, ruido=0.0):
        x = tl(largo)
        v = np.sin(2 * math.pi * f * x)
        if ruido:
            v = v + rng.normal(0, ruido, len(x))
        return v * np.exp(-caida * x)

    # un colchon grave que no se va en todo el montaje
    x = tl(TOTAL - 4.0)
    lecho = (np.sin(2 * math.pi * 55 * x) * 0.13
             + np.sin(2 * math.pi * 82.41 * x) * 0.08
             + np.sin(2 * math.pi * 110 * x) * 0.05)
    lecho *= 0.55 + 0.45 * np.sin(2 * math.pi * x / 11.0)
    en(4.0, lecho * np.clip(x / 1.5, 0, 1) * np.clip((TOTAL - 4.0 - x) / 2.0, 0, 1))

    # el tictac del reloj, que sigue latiendo bajo todo
    t = 6.2
    while t < 30.0:
        en(t, golpe(0.05, 2100 if int((t - 6.2) / 0.7) % 2 == 0 else 1600, 80), 0.10)
        t += 0.7

    # 10,8 el segundo estallido: los fragmentos salen disparados
    x = tl(2.6)
    cristal = rng.normal(0, 1, len(x)) * np.exp(-3.2 * x) * 0.5
    cristal *= 0.45 + 0.55 * np.sin(2 * math.pi * 4800 * x)
    en(10.75, cristal, 0.75)
    en(10.75, golpe(2.6, 40, 1.7), 0.8)

    # 12,4 / 18,4 / 21,7 los tres fogonazos, con su sonido de era
    en(12.35, golpe(2.4, 196, 2.2) + golpe(2.4, 392, 3.0) * 0.4, 0.42)   # campana
    en(18.35, golpe(1.4, 58, 5.0, ruido=0.30), 0.42)                     # maquinaria
    x = tl(1.2)
    en(21.65, np.sign(np.sin(2 * math.pi * 900 * x)) * np.exp(-8.0 * x), 0.16)   # digital

    # 24,0 el negro: un respiro, y Ekkar entrando
    x = tl(4.2)
    en(24.15, (np.sin(2 * math.pi * 98 * x) * 0.4 + np.sin(2 * math.pi * 146.83 * x) * 0.22)
       * np.clip(x / 1.2, 0, 1) * np.clip((4.2 - x) / 1.0, 0, 1), 0.5)

    # 28,4 el tajo
    x = tl(1.0)
    en(28.35, np.sin(2 * math.pi * 3000 * x * (1 + 0.45 * x)) * np.exp(-6.5 * x) * 0.30
       + rng.normal(0, 1, len(x)) * np.exp(-20 * x) * 0.16)
    en(28.45, golpe(2.2, 130.81, 1.6) * 0.5 + golpe(2.2, 196, 1.9) * 0.3, 0.5)

    # el cierre, que sostiene la cara y el titulo
    x = tl(4.2)
    en(28.9, (np.sin(2 * math.pi * 65.41 * x) * 0.5 + np.sin(2 * math.pi * 98 * x) * 0.24
              + np.sin(2 * math.pi * 130.81 * x) * 0.12)
       * np.clip(x / 0.8, 0, 1) * np.exp(-0.45 * x), 0.62)

    out[-int(0.5 * SR):] *= np.linspace(1, 0, int(0.5 * SR))
    pico = np.max(np.abs(out))
    if pico > 0:
        out *= 0.80 / pico
    return out


def escribe_wav(ruta, data, canales=1):
    pcm = (np.clip(data, -1, 1) * 32767).astype(np.int16)
    with wave.open(ruta, "wb") as w:
        w.setnchannels(canales)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(struct.pack("<%dh" % len(pcm), *pcm))


# ----------------------------------------------------------------------- main

def lee_video(ruta):
    """Va soltando los fotogramas del video, uno a uno, sin cargarlo entero."""
    cmd = ["ffmpeg", "-loglevel", "error", "-i", ruta,
           "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{ANCHO}x{ALTO}", "-"]
    p = subprocess.Popen(cmd, stdout=subprocess.PIPE, bufsize=ANCHO * ALTO * 3)
    tam = ANCHO * ALTO * 3
    while True:
        crudo = p.stdout.read(tam)
        if len(crudo) < tam:
            break
        yield np.frombuffer(crudo, dtype=np.uint8).reshape(ALTO, ANCHO, 3)
    p.stdout.close()
    p.wait()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--solo-apertura", action="store_true")
    ap.add_argument("--solo-pista", action="store_true",
                    help="saca solo la pista de 33 s y no toca el video")
    args = ap.parse_args()

    os.makedirs(os.path.dirname(SALIDA), exist_ok=True)

    if args.solo_pista:
        print("Sintetizando la pista completa de 33 s...")
        escribe_wav(PISTA, pista_completa())
        print(f"  -> {PISTA}")
        return
    tmp_audio = os.path.join(os.path.dirname(SALIDA), "_audio_final.wav")

    # ---- audio: los 6 s nuevos delante del audio que ya traia el video
    print("Sonido de la apertura...")
    ap_wav = os.path.join(os.path.dirname(SALIDA), "_apertura.wav")
    escribe_wav(ap_wav, sonido_apertura())

    if not args.solo_apertura:
        print("Pegando el audio original detras...")
        subprocess.run(
            ["ffmpeg", "-y", "-loglevel", "error", "-i", ap_wav, "-i", ENTRADA,
             "-filter_complex",
             "[0:a]aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=stereo[a];"
             "[1:a]aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=stereo[b];"
             "[a][b]concat=n=2:v=0:a=1[out]",
             "-map", "[out]", tmp_audio], check=True)
    else:
        tmp_audio = ap_wav

    # ---- video
    total = int((APERTURA if args.solo_apertura else TOTAL) * FPS)
    salida = SALIDA if not args.solo_apertura else SALIDA.replace(".mp4", "_apertura.mp4")
    cmd = ["ffmpeg", "-y", "-loglevel", "error",
           "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{ANCHO}x{ALTO}", "-r", str(FPS), "-i", "-",
           "-i", tmp_audio,
           "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "19", "-preset", "medium",
           "-c:a", "aac", "-b:a", "192k", "-shortest", salida]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)

    print("Fabricando la apertura...")
    apertura = Apertura()
    rng = np.random.default_rng(11)
    fuente_video = None if args.solo_apertura else lee_video(ENTRADA)

    for i in range(total):
        t = i / FPS
        if t < APERTURA:
            a = apertura.fotograma(t)
        else:
            crudo = next(fuente_video, None)
            if crudo is None:
                break
            a = acabado(crudo, rng)

        im = Image.fromarray(a)
        for entra, nombre, sub, baja in CARTELES:
            if entra <= t <= entra + DURA_CARTEL:
                im, destello = cartel(im, nombre, sub, t - entra, baja)
                if destello > 0:
                    im = Image.fromarray(tinta(np.asarray(im), ORO, destello))

        proc.stdin.write(im.tobytes())
        if (i + 1) % 90 == 0:
            print(f"  {i + 1}/{total}")

    proc.stdin.close()
    if proc.wait() != 0:
        sys.exit("ffmpeg ha fallado.")

    tam = os.path.getsize(salida) / 1024 / 1024
    print(f"\n  {salida}  ({tam:.1f} MB)")

    for basura in (ap_wav, tmp_audio):
        if os.path.exists(basura) and basura != salida:
            try:
                os.remove(basura)
            except OSError:
                pass


if __name__ == "__main__":
    main()
