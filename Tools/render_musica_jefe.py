# -*- coding: utf-8 -*-
"""
Saca a wav el tema del jefe, para poder oirlo sin abrir Unity.

BossMusic.cs genera su musica por codigo, con una semilla fija y sin nada de
aleatorio de verdad: el mismo clip sale siempre. Esto es esa misma sintesis,
paso por paso, incluido el System.Random de .NET, asi que el wav que sale es
el que suena en la pelea.

Uso:
    python Tools/render_musica_jefe.py
    python Tools/render_musica_jefe.py --vueltas 2 --fase 2
"""

import argparse
import math
import os
import struct
import wave

import numpy as np

SR = 44100
LOOP_SEGUNDOS = 19.2
SEMILLA = 4093
NEGRA = 0.4                      # 150 pulsos por minuto


class RandomDeNet:
    """El System.Random de .NET, que es lo que usa Unity. Mismo seed, mismos numeros."""

    MBIG = 2147483647
    MSEED = 161803398

    def __init__(self, seed):
        self.tabla = [0] * 56
        resta = self.MBIG if seed == -2147483648 else abs(seed)
        mj = self.MSEED - resta
        self.tabla[55] = mj
        mk = 1
        for i in range(1, 55):
            ii = (21 * i) % 55
            self.tabla[ii] = mk
            mk = mj - mk
            if mk < 0:
                mk += self.MBIG
            mj = self.tabla[ii]
        for _ in range(1, 5):
            for i in range(1, 56):
                self.tabla[i] -= self.tabla[1 + (i + 30) % 55]
                if self.tabla[i] < 0:
                    self.tabla[i] += self.MBIG
        self.inext, self.inextp = 0, 21

    def next_double(self):
        self.inext = 1 if self.inext + 1 >= 56 else self.inext + 1
        self.inextp = 1 if self.inextp + 1 >= 56 else self.inextp + 1
        v = self.tabla[self.inext] - self.tabla[self.inextp]
        if v == self.MBIG:
            v -= 1
        if v < 0:
            v += self.MBIG
        self.tabla[self.inext] = v
        return v * (1.0 / self.MBIG)


def construye():
    """El equivalente exacto de BossMusic.Construye()."""
    n = round(LOOP_SEGUNDOS * SR)
    t = np.arange(n, dtype=np.float64) / SR
    dur = n / SR
    tau = 2 * math.pi

    # bajo obstinado: corchea seca, siempre la misma nota
    paso = np.mod(t, NEGRA * 0.5)
    puerta = np.exp(-9.0 * paso)
    data = np.sin(tau * 55.0 * t) * 0.55 * puerta
    data += np.sin(tau * 110.0 * t) * 0.18 * puerta

    # la segunda menor: dos notas casi iguales que baten entre si
    data += ((np.sin(tau * 233.08 * t) + np.sin(tau * 246.94 * t)) * 0.055
             * (0.55 + 0.45 * np.sin(tau * t / dur * 3.0)))

    # colchon grave
    data += np.sin(tau * 82.41 * t) * 0.10

    tictac(data)
    golpes(data, NEGRA * 8.0)
    aire(data)
    bucle(data)
    normaliza(data, 0.62)
    return data


def tictac(data):
    paso = round(NEGRA * SR)
    largo = round(0.035 * SR)
    tl = np.arange(largo) / SR
    env = np.exp(-90.0 * tl)
    for k, inicio in enumerate(range(0, len(data), paso)):
        f = 2400.0 if k % 2 == 0 else 1750.0
        trozo = np.sin(2 * math.pi * f * tl) * env * 0.14
        fin = min(inicio + largo, len(data))
        data[inicio:fin] += trozo[: fin - inicio]


def golpes(data, cada):
    paso = round(cada * SR)
    largo = round(cada * 0.9 * SR)
    tl = np.arange(largo) / SR
    env = np.exp(-1.9 * tl) * np.clip(np.arange(largo) / (SR * 0.004), 0, 1)
    v = (np.sin(2 * math.pi * 110.0 * tl)
         + np.sin(2 * math.pi * 164.81 * tl) * 0.55
         + np.sin(2 * math.pi * 220.9 * tl) * 0.30)
    trozo = v * env * 0.13
    for inicio in range(0, len(data), paso):
        fin = min(inicio + largo, len(data))
        data[inicio:fin] += trozo[: fin - inicio]


def aire(data):
    rng = RandomDeNet(SEMILLA)
    n = len(data)
    ruido = np.fromiter((rng.next_double() * 2.0 - 1.0 for _ in range(n)),
                        dtype=np.float64, count=n)
    # el mismo filtro paso bajo de una linea que hay en el C#
    lp = np.empty(n)
    acc = 0.0
    for i in range(n):
        acc += (ruido[i] - acc) * 0.008
        lp[i] = acc
    t = np.arange(n) / SR
    dur = n / SR
    data += lp * 0.26 * (0.45 + 0.55 * np.sin(2 * math.pi * t / dur * 2.0))


def bucle(data):
    fade = min(len(data) // 4, round(0.10 * SR))
    k = np.arange(fade) / fade
    data[:fade] *= k
    data[len(data) - fade:] *= k[::-1]


def normaliza(data, objetivo):
    pico = np.max(np.abs(data))
    if pico < 0.0001:
        return
    data *= objetivo / pico


def escribe_wav(ruta, data, pitch=1.0):
    if abs(pitch - 1.0) > 1e-6:
        # asi es como suena con el pitch de la fase: mismo clip, mas corto
        n = int(len(data) / pitch)
        idx = np.clip((np.arange(n) * pitch).astype(int), 0, len(data) - 1)
        data = data[idx]
    pcm = np.clip(data, -1.0, 1.0)
    pcm = (pcm * 32767).astype(np.int16)
    with wave.open(ruta, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(struct.pack("<%dh" % len(pcm), *pcm))


def informe(data):
    pico = float(np.max(np.abs(data)))
    rms = float(np.sqrt(np.mean(data ** 2)))
    silencio = float(np.mean(np.abs(data) < 0.01))
    print(f"  duracion      {len(data) / SR:.2f} s")
    print(f"  pico          {pico:.3f}   ({20 * math.log10(max(pico, 1e-9)):+.1f} dBFS)")
    print(f"  RMS           {rms:.3f}   ({20 * math.log10(max(rms, 1e-9)):+.1f} dBFS)")
    print(f"  casi mudo     {silencio:.1%} de las muestras")
    print(f"  costura       entrada {abs(data[0]):.4f}  salida {abs(data[-1]):.4f}")

    # que el tictac este de verdad: energia alrededor de 2400 y 1750 Hz
    espectro = np.abs(np.fft.rfft(data[: SR * 4] * np.hanning(SR * 4)))
    frec = np.fft.rfftfreq(SR * 4, 1 / SR)
    for nombre, f in (("bajo 55 Hz", 55), ("pad 82 Hz", 82.41),
                      ("roce 233/247 Hz", 240), ("tictac 1750 Hz", 1750),
                      ("tictac 2400 Hz", 2400)):
        cerca = (frec > f * 0.97) & (frec < f * 1.03)
        print(f"  {nombre:<18} energia {espectro[cerca].max():9.1f}")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--vueltas", type=int, default=1, help="cuantas veces se repite el bucle")
    p.add_argument("--fase", type=int, default=1, help="1 o 2: la fase 2 sube el tono un 4,5%%")
    p.add_argument("--salida", default=None)
    args = p.parse_args()

    print("Sintetizando el tema del jefe (BossMusic.cs)...")
    data = construye()
    informe(data)

    pitch = 1.0 + min(max(args.fase - 1, 0), 3) * 0.045
    salida = args.salida or os.path.join(
        os.path.dirname(os.path.abspath(__file__)), "out",
        f"musica_jefe_fase{args.fase}.wav")
    os.makedirs(os.path.dirname(salida), exist_ok=True)

    escribe_wav(salida, np.tile(data, args.vueltas), pitch)
    print(f"\n  fase {args.fase}, tono x{pitch:.3f}")
    print(f"  -> {salida}")


if __name__ == "__main__":
    main()
