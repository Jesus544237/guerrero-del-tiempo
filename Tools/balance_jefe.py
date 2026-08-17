# -*- coding: utf-8 -*-
"""
Cuanto dura la pelea con el Senor del Tiempo, y cuanto dano se lleva Ekkar.

Esto NO sustituye a jugarla: es el mismo reloj y los mismos numeros que hay en
el codigo, corridos muchas veces, para ver si la pelea aguanta de pie antes de
sentarse a probarla. Lo que modela:

  - la cadencia del jefe, su reparto de ataques al azar y lo que saca cada uno
    (Assets/_Game/Scripts/Gameplay/BossAttackPatterns.cs)
  - los fotogramas de invulnerabilidad de los dos, que es lo que de verdad
    decide cuanto dano entra en las rafagas de muchos proyectiles
  - las dos fases: al vaciar la barra el jefe se transforma y vuelve entero
  - tres formas de jugar, para ver el suelo y el techo

Lo que NO modela: puntería, plataformas, el detener el tiempo, y si Ekkar
llega o no a meterse en su alcance. El "acierto" es un parametro.

Uso:
    python Tools/balance_jefe.py
    python Tools/balance_jefe.py --vida-jefe 18 --chrono-tope 4
"""

import argparse
import random
import statistics

# --------------------------------------------------------------- los numeros
# Todos salidos de EnemySetup.cs, EkkarController.cs, PlayerCombat.cs y
# BossAttackPatterns.cs. Si los cambias ahi, cambialos aqui.

EKKAR_VIDA = 8
EKKAR_INVULN = 1.0            # Damageable.invulnerable del jugador
AUTO_DANO, AUTO_CADENCIA = 1, 0.42
CARGADO_DANO, CARGADO_COSTE, CARGADO_CURA = 3, 4, 1
MANA_MAX, MANA_INICIAL, MANA_POR_GOLPE = 10, 4, 1
CHRONO_COSTE, CHRONO_CD = 6, 16.0
CHRONO_DEJA_EN, CHRONO_REMATA = 0.22, 0.30

JEFE_VIDA = 24
JEFE_FASES = 2
JEFE_INVULN = 0.35
TRANSFORMACION = 16 / 14 + 0.35

# (nombre, cuantos impactos puede meter, dano de cada uno)
FASE1 = [("pendulo", 1, 2), ("agujas", 3, 1), ("eco", 1, 1)]
FASE2 = [("grieta", 2, 2), ("reloj", 8, 1), ("enjambre", 4, 1), ("final", 11, 1)]
CADENCIA = {1: 3.4, 2: 2.6}


def dano_que_entra(impactos, dano, invuln, cadencia_rafaga=0.12):
    """
    Una rafaga de N proyectiles no hace N x dano: entre uno y el siguiente
    pasan centesimas, y el que recibe esta invulnerable un segundo entero.
    Solo cuenta el primero que toca.
    """
    entra, reloj, siguiente_valido = 0, 0.0, 0.0
    for _ in range(impactos):
        if reloj >= siguiente_valido:
            entra += dano
            siguiente_valido = reloj + invuln
        reloj += cadencia_rafaga
    return entra


def pelea(estilo, acierto_ekkar, acierto_jefe, semilla=None, tope_chrono=None,
          cura_en_apuros=False, chrono_tope_dano=None):
    """Devuelve (segundos, dano recibido, veces que uso el chronobreak)."""
    rnd = random.Random(semilla)

    t = 0.0
    vida_ekkar, mana = EKKAR_VIDA, MANA_INICIAL
    ekkar_invuln_hasta = -99.0
    fase, vida_jefe = 1, JEFE_VIDA
    jefe_invuln_hasta = -99.0
    proximo_golpe, proximo_ataque_jefe = 0.0, CADENCIA[1]
    chrono_listo, chronos = 0.0, 0
    recibido = 0
    golpes_seguidos = 0

    while t < 600:
        t = min(proximo_golpe, proximo_ataque_jefe)

        # ---------------------------------------------------------- Ekkar
        if t >= proximo_golpe:
            usa_chrono = (
                estilo == "chronobreak"
                and t >= chrono_listo
                and mana >= CHRONO_COSTE
                and (tope_chrono is None or chronos < tope_chrono)
            )
            if usa_chrono:
                mana -= CHRONO_COSTE
                chrono_listo = t + CHRONO_CD
                chronos += 1
                if chrono_tope_dano is not None:
                    # el tope que se propone para los jefes: un mordisco fijo,
                    # no un porcentaje de su barra
                    vida_jefe -= chrono_tope_dano
                else:
                    limite = max(1, -(-JEFE_VIDA * 30 // 100))  # ceil(24*0.30)=8
                    deja = max(1, -(-JEFE_VIDA * 22 // 100))    # ceil(24*0.22)=6
                    vida_jefe = 0 if vida_jefe <= limite else deja
                proximo_golpe = t + 1.1                          # el clip bloquea
            else:
                cargado = (
                    estilo in ("cargado", "chronobreak")
                    and mana >= CARGADO_COSTE
                    and golpes_seguidos >= CARGADO_COSTE
                )
                acierta = rnd.random() < acierto_ekkar and t >= jefe_invuln_hasta
                if cargado:
                    mana -= CARGADO_COSTE
                    golpes_seguidos = 0
                    if acierta:
                        vida_jefe -= CARGADO_DANO
                        jefe_invuln_hasta = t + JEFE_INVULN
                        if not cura_en_apuros or vida_ekkar <= EKKAR_VIDA // 2:
                            vida_ekkar = min(EKKAR_VIDA, vida_ekkar + CARGADO_CURA)
                elif acierta:
                    vida_jefe -= AUTO_DANO
                    jefe_invuln_hasta = t + JEFE_INVULN
                    mana = min(MANA_MAX, mana + MANA_POR_GOLPE)
                    golpes_seguidos += 1
                proximo_golpe = t + AUTO_CADENCIA

            if vida_jefe <= 0:
                if fase >= JEFE_FASES:
                    return t, recibido, chronos
                fase += 1
                vida_jefe = JEFE_VIDA
                t += TRANSFORMACION
                proximo_golpe = t
                proximo_ataque_jefe = t + CADENCIA[fase]
                continue

        # ----------------------------------------------------------- jefe
        if t >= proximo_ataque_jefe:
            _nombre, impactos, dano = rnd.choice(FASE1 if fase == 1 else FASE2)
            if rnd.random() < acierto_jefe and t >= ekkar_invuln_hasta:
                entra = dano_que_entra(impactos, dano, EKKAR_INVULN)
                vida_ekkar -= entra
                recibido += entra
                ekkar_invuln_hasta = t + EKKAR_INVULN
                if vida_ekkar <= 0:
                    return None, recibido, chronos       # Ekkar cae
            proximo_ataque_jefe = t + CADENCIA[fase]

    return None, recibido, chronos


def tanda(estilo, acierto_ekkar, acierto_jefe, veces=400, tope_chrono=None,
          cura_en_apuros=False, chrono_tope_dano=None):
    duraciones, danos, muertes, chronos = [], [], 0, []
    for i in range(veces):
        seg, rec, ch = pelea(estilo, acierto_ekkar, acierto_jefe, semilla=i,
                             tope_chrono=tope_chrono,
                             cura_en_apuros=cura_en_apuros,
                             chrono_tope_dano=chrono_tope_dano)
        if seg is None:
            muertes += 1
        else:
            duraciones.append(seg)
            danos.append(rec)
            chronos.append(ch)
    return duraciones, danos, muertes, chronos


def main():
    global JEFE_VIDA

    p = argparse.ArgumentParser()
    p.add_argument("--veces", type=int, default=400)
    p.add_argument("--vida-jefe", type=int, default=JEFE_VIDA)
    p.add_argument("--chrono-tope", type=int, default=None,
                   help="cuantos chronobreaks como mucho (para probar un tope)")
    p.add_argument("--cura-en-apuros", action="store_true",
                   help="el golpe cargado solo cura si Ekkar va por debajo de la mitad")
    p.add_argument("--chrono-dano-jefe", type=int, default=None,
                   help="el chronobreak quita esto a un jefe, en vez del 78%% de su barra")
    args = p.parse_args()

    JEFE_VIDA = args.vida_jefe

    print(f"Jefe: {JEFE_VIDA} de vida x {JEFE_FASES} fases = {JEFE_VIDA * JEFE_FASES}")
    print(f"Ekkar: {EKKAR_VIDA} de vida, {EKKAR_INVULN}s de gracia\n")
    print(f"{'estilo':<26}{'acierto':>9}{'dura':>10}{'recibe':>9}{'muere':>8}{'chronos':>9}")
    print("-" * 71)

    casos = [
        ("solo autoataque", "auto", 0.95),
        ("solo autoataque, torpe", "auto", 0.60),
        ("auto + golpe cargado", "cargado", 0.95),
        ("auto + cargado, torpe", "cargado", 0.60),
        ("con chronobreak", "chronobreak", 0.95),
        ("con chronobreak, torpe", "chronobreak", 0.60),
    ]
    for etiqueta, estilo, acierto in casos:
        dur, dan, mue, chr_ = tanda(estilo, acierto, 0.75, args.veces, args.chrono_tope,
                                    args.cura_en_apuros, args.chrono_dano_jefe)
        if not dur:
            print(f"{etiqueta:<26}{acierto:>9.0%}{'-':>10}{'-':>9}{mue / args.veces:>7.0%}{'-':>9}")
            continue
        print(f"{etiqueta:<26}{acierto:>9.0%}"
              f"{statistics.mean(dur):>9.1f}s"
              f"{statistics.mean(dan):>9.1f}"
              f"{mue / args.veces:>7.0%}"
              f"{statistics.mean(chr_):>9.1f}")

    print("\nEl jefe acierta el 75% de sus ataques. 'torpe' = Ekkar falla 4 de cada 10.")
    print("'recibe' es dano total encajado; Ekkar tiene 8 y el cargado le cura.")


if __name__ == "__main__":
    main()
