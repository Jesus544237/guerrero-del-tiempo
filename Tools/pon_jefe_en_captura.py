# -*- coding: utf-8 -*-
"""
Mete al Senor del Tiempo en fase 2 dentro de una captura del juego.

Para que hace falta: la captura jefe_fasee2.png tiene la composicion buena — el
portal de engranajes, el cartel del jefe, el HUD entero y Ekkar atacando — pero
el jefe no salio en ella. El aspecto de fase 2 solo existe en un render con
fondo verde. Esto quita el verde y lo coloca donde tocaria, respetando dos
cosas que si se rompen cantan:

  - el cartel del jefe es interfaz y va POR ENCIMA de todo. Como el cartel es
    un velo oscuro semitransparente, no vale con pegar y ya: hay que velar
    tambien al jefe en esa franja y devolver encima las letras y las reglas
    doradas, que se recuperan de la propia captura por su color.
  - el jefe se apoya en el suelo, no flota: los pies van clavados a la linea
    de la plataforma.

Uso:
    python Tools/pon_jefe_en_captura.py
    python Tools/pon_jefe_en_captura.py --alto 380 --centro-x 300 --suelo 540
"""

import argparse
import os

import numpy as np
from PIL import Image, ImageFilter

PROY = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
IMAGENES = os.path.join(
    r"C:\Users\usuario\Documents\SENA\programacion de video juegos",
    "Fase 2 - Planeacion",
    "GA2-220501127-AA1-EV01 - Documento tecnico requerimientos de desarrollo",
    "fuente editable", "imagenes")
CAPTURA = os.path.join(IMAGENES, "jefe_fasee2.png")
VERDE = os.path.join(PROY, "Tools", "prompts", "hora_cero", "enemigos",
                     "senor_del_tiempo", "videos",
                     "senor_del_tiempo_fase2_ASPECTO_fondo_verde_1.jpg")

VELO = np.array([10, 5, 20], np.float32)     # el color de la franja del cartel
ALFA_VELO = 0.78                             # lo que tapa, sacado de BossBanner


def quita_verde(ruta):
    """Recorta las bandas negras, quita el croma verde y devuelve RGBA ajustado."""
    im = Image.open(ruta).convert("RGB")
    a = np.asarray(im).astype(np.float32)

    # Fuera las bandas negras de arriba y abajo. No vale mirar el brillo medio:
    # las filas del borde son medio negras y medio verdes, pasan el corte, y
    # como aqui lo oscuro se toma por opaco acababan de barra negra bajo el
    # jefe. Se busca directamente donde hay croma: una fila es del cuadro si
    # una cuarta parte de ella es verde.
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    es_verde = (G > 90) & (G > R + 25) & (G > B + 25)
    vivas = np.nonzero(es_verde.mean(axis=1) > 0.25)[0]
    if len(vivas):
        a = a[vivas.min():vivas.max() + 1]
        R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    # verde de croma: G manda con claridad sobre los otros dos
    exceso = G - np.maximum(R, B)
    alfa = np.clip(1.0 - (exceso - 12) / 45.0, 0, 1)
    alfa[G < 70] = 1.0                       # lo oscuro nunca es croma

    # quitar el reflejo verde del borde: donde G se pasa, se baja al vecino
    tope = np.maximum(R, B) + 18
    G = np.minimum(G, np.where(exceso > 0, tope, G))
    a = np.dstack([R, G, B])

    rgba = np.dstack([a, alfa * 255]).astype(np.uint8)
    im = Image.fromarray(rgba, "RGBA")

    # un pelin de suavizado en el alfa para que no quede el borde de sierra
    canal = im.getchannel("A").filter(ImageFilter.GaussianBlur(0.8))
    im.putalpha(canal)

    caja = im.getbbox()
    return im.crop(caja) if caja else im


def franja_del_cartel(a):
    """
    Encuentra la franja del cartel por sus dos reglas doradas.

    Hay que hilar fino: la barra de vida del jefe, arriba, tambien es una fila
    larga de oro y se colaba. Las reglas del cartel se distinguen en tres
    cosas — caen en la mitad de la imagen, miden lo mismo las dos (el 40 % del
    ancho, que es lo que dibuja BossBanner), y van una encima de otra a la
    misma distancia del centro.
    """
    alto, ancho = a.shape[0], a.shape[1]
    R, G, B = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    oro = (R > 170) & (G > 120) & (G < R) & (B < 110)

    candidatas = []
    for y in range(int(alto * 0.2), int(alto * 0.85)):
        n = oro[y].sum()
        if ancho * 0.30 < n < ancho * 0.55:
            candidatas.append(y)
    if len(candidatas) < 2:
        return None

    # el borde dorado de la plataforma tambien pasa el filtro, asi que no vale
    # coger la primera y la ultima: se busca la pareja cuya separacion es la
    # del cartel, y de paso la que quede mas centrada en la imagen
    esperada = alto * 0.178
    mejor, coste_mejor = None, None
    for i, arriba in enumerate(candidatas):
        for abajo in candidatas[i + 1:]:
            if abajo - arriba < alto * 0.05:
                continue                  # son la misma regla, de 2 px de alto
            centro = (arriba + abajo) / 2
            coste = abs((abajo - arriba) - esperada) + abs(centro - alto / 2) * 0.5
            if coste_mejor is None or coste < coste_mejor:
                mejor, coste_mejor = (arriba, abajo), coste
    if mejor is None or abs((mejor[1] - mejor[0]) - esperada) > alto * 0.06:
        return None

    arriba, abajo = mejor
    centro = (arriba + abajo) // 2
    media = int(alto * 0.213 / 2)         # la franja mide 230 de 1080
    return max(0, centro - media), min(alto, centro + media)


def mascara_interfaz(a, y0, y1):
    """Las letras y las reglas del cartel, para devolverlas por encima."""
    trozo = a[y0:y1]
    R, G, B = trozo[:, :, 0], trozo[:, :, 1], trozo[:, :, 2]
    oro = (R > 150) & (G > 100) & (B < 120) & (R > B + 60)
    hueso = (R > 165) & (G > 165) & (B > 165)
    m = (oro | hueso).astype(np.float32)
    # se engorda un pixel para no dejar el texto con el borde comido
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8))
                   .filter(ImageFilter.MaxFilter(3))).astype(np.float32) / 255.0
    return m


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--alto", type=int, default=360, help="alto del jefe en pixeles")
    p.add_argument("--centro-x", type=int, default=300)
    p.add_argument("--suelo", type=int, default=540, help="donde apoya los pies")
    p.add_argument("--salida", default=None)
    args = p.parse_args()

    base = Image.open(CAPTURA).convert("RGB")
    original = np.asarray(base).astype(np.float32)
    W, H = base.size

    jefe = quita_verde(VERDE)
    escala = args.alto / jefe.height
    jefe = jefe.resize((max(1, int(jefe.width * escala)), args.alto), Image.LANCZOS)
    print(f"jefe recortado y escalado a {jefe.size}")

    x = args.centro_x - jefe.width // 2
    y = args.suelo - jefe.height

    lienzo = base.convert("RGBA")
    lienzo.alpha_composite(jefe, (x, y))
    salida = np.asarray(lienzo.convert("RGB")).astype(np.float32)

    # donde ha caido el jefe, para poder velarlo dentro de la franja
    puesto = Image.new("L", (W, H), 0)
    puesto.paste(jefe.getchannel("A"), (x, y))
    puesto = np.asarray(puesto).astype(np.float32) / 255.0

    franja = franja_del_cartel(original)
    if franja is None:
        print("  ! no encuentro las reglas del cartel; se deja sin velar")
    else:
        y0, y1 = franja
        print(f"franja del cartel: y {y0}-{y1}")

        # 1) el jefe, dentro de la franja, se va detras del velo
        m = puesto[y0:y1][:, :, None]
        trozo = salida[y0:y1]
        velado = trozo * (1 - ALFA_VELO) + VELO * ALFA_VELO
        salida[y0:y1] = trozo * (1 - m) + velado * m

        # 2) y las letras y las reglas vuelven encima, tal cual estaban
        ui = mascara_interfaz(original, y0, y1)[:, :, None]
        salida[y0:y1] = salida[y0:y1] * (1 - ui) + original[y0:y1] * ui

    destino = args.salida or os.path.join(IMAGENES, "jefe_fasee2.png")
    Image.fromarray(np.clip(salida, 0, 255).astype(np.uint8)).save(destino)
    print(f"-> {destino}")


if __name__ == "__main__":
    main()
