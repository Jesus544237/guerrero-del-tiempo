# -*- coding: utf-8 -*-
"""
Genera los prompts de IMAGEN de los obstaculos y de las particulas de cada era.

A diferencia de los enemigos, esto no es video: son sprites sueltos. Los
obstaculos son props con los que Ekkar choca, se sube o se quema, y las
particulas son las motas que caen por la pantalla, que el juego ya mueve por
codigo (EmberFall, SuspendedAsh, ParticleField). Por eso se piden como imagen
fija: el movimiento lo pone Unity, no el generador.

Todo va a escala del juego: 100 pixeles = 1 unidad de Unity, y Ekkar mide
240 px. Los tamanos de cada prompt estan pensados con esa regla, asi que lo
generado entra en la escena sin reescalar a ojo.

Uso:
    python Tools/make_obstacle_prompts.py
"""

import os

HERE = os.path.dirname(os.path.abspath(__file__))
PROMPTS = os.path.join(HERE, "prompts")

# --------------------------------------------------------------- bloques fijos

ESTILO = """RENDERING: 2D pixel art, 16-bit era style, crisp hard pixel edges and subtle dithering, drawn as a side-view game sprite. NO motion blur, NO anti-aliasing smear, NO film grain, NO lens flare, NO depth of field, NO cinematic colour grading, NO 3D render look. It must look like a sprite cut out of a 2D platformer sprite sheet.

Colour palette, strictly: void #0A0515, deep purple #1A0A2E, mid purple #2D1B69, violet #7C3AED, cyan glow #06B6D4, bright cyan #22D3EE, gold #F59E0B, light gold #FBBF24, blood red #DC2626, cold white #E2E8F0."""

FONDO = """BACKGROUND: pure flat solid chroma green #00FF00 filling the entire image, absolutely uniform. No gradient, no vignette, no ground line, no floor, no shadow cast on the background, no scenery, no props other than the object itself. Chroma green must never appear anywhere on the object itself or in its glow."""

ENCUADRE = """FRAMING: exactly ONE object, seen from a strict SIDE VIEW, centred in the image with a little empty green margin all around it. Nothing is cut off by the edges of the image. Do not draw a scene, do not draw a character, do not draw the same object twice."""

NEGATIVOS = """Hard requirements: one single object, no characters, no creatures, no hands, no other props, no text, no letters, no numbers, no user interface, no logos, no watermark, no frame, no border, no drop shadow on the background."""

ESCALA = """SCALE: the hero of this game is 240 pixels tall on screen. Draw this object so that it reads at {alto} pixels tall and roughly {ancho} pixels wide next to him, with the level of pixel detail that size allows."""


def build(nombre, desc, ancho, alto, extra=""):
    partes = [ESTILO, "", f"SUBJECT: {desc}", "",
              ESCALA.format(ancho=ancho, alto=alto), "", ENCUADRE, "", FONDO]
    if extra:
        partes += ["", extra]
    partes += ["", NEGATIVOS]
    return "\n".join(partes) + "\n"


# ----------------------------------------------------------------- obstaculos
#
# (archivo, ancho, alto, para que sirve en el juego, descripcion)
# El "para que sirve" no va al prompt: va al indice, para saber luego que hace
# cada pieza cuando toque montarlas en la escena.

OBSTACULOS = {
    "medieval": [
        ("roca_catapulta", 170, 170, "plataforma",
         "A boulder from a siege catapult, caught in mid-flight and frozen in time: a rough chunk of grey-brown rock, chipped and cracked, wrapped in a thin skin of pale cyan frost, with a few small stone chips hanging motionless around it as if the air held them. Cold cyan light seeps out of the cracks."),
        ("flechas_congeladas", 280, 110, "plataforma",
         "A tight cluster of about a dozen medieval war arrows frozen in mid-flight, all pointing the same way, packed close enough to stand on, their shafts wooden and their fletching torn and blood-red. A thin cyan glow runs along each shaft and a few frozen sparks hang between them."),
        ("escalera_asedio", 150, 430, "plataforma",
         "A medieval wooden siege ladder, tall and narrow, its rails scorched black and its rungs splintered, with iron hooks at the top and a torn blood-red banner scrap tied near the middle. Ash clings to the wood."),
        ("rastrillo", 300, 380, "obstaculo",
         "A castle portcullis: a heavy iron grate of thick vertical bars crossed by horizontal bands, rusted and bent, with cruel spikes along its bottom edge and broken chain links hanging from its top corners. One gold clock gear is welded into the middle of the grate."),
        ("pinchos_asedio", 250, 100, "peligro",
         "A row of broken spears and splintered wooden stakes driven into the ground at an angle, points upward, their iron tips rusted and notched, with torn cloth scraps and old ash gathered around their bases."),
        ("brasero", 130, 180, "peligro",
         "A medieval iron brazier on three legs, its bowl full of burning coals, with tall orange and gold flames rising from it and a few embers floating just above. The iron is black and pitted, the fire is the brightest thing in the sprite."),
        ("muro_roto", 380, 220, "plataforma",
         "A broken chunk of castle wall lying as rubble: stacked grey stone blocks with mortar crumbling between them, the top edge jagged where it snapped, moss and soot on the stones, and one narrow arrow-slit window still intact on one side."),
    ],
    "industrial": [
        ("piston", 190, 320, "obstaculo",
         "A heavy foundry piston hanging from above: a thick riveted iron cylinder with a polished steel shaft coming out of its bottom end and a broad flat crushing head at the tip, brass pressure pipes running along its side and a small round gauge with a cracked glass face."),
        ("engranaje_plataforma", 320, 320, "plataforma",
         "An enormous industrial gear standing on its edge, big enough to walk on: cast iron teeth around the rim, a heavy hub with six spokes, rust bleeding down its face, brass bolts at the hub and a faint amber glow of furnace light caught in its grooves."),
        ("pasarela", 420, 90, "plataforma",
         "A segment of industrial metal catwalk seen from the side: a riveted steel deck plate with a diamond-tread surface, a thin handrail of pipe along the back edge, and two short support brackets underneath. Soot-stained, with rust at the joints."),
        ("valvula_vapor", 180, 140, "peligro",
         "A brass steam valve bolted to the ground, its wheel handle rusted, with a short wide nozzle pointing straight up and a hard jet of white steam blasting out of it. The steam is drawn as solid pixel-art plumes, not a soft cloud."),
        ("crisol", 380, 140, "peligro",
         "A shallow foundry channel full of molten metal seen from the side: an iron trough with a rim of blackened brick, filled with glowing orange and yellow liquid metal, its surface bright and its edges crusted with dark slag. Small embers rise from it."),
        ("prensa", 300, 380, "obstaculo",
         "A foundry stamping press: a squat iron frame with two vertical guide rails, a massive flat hammer block held at the top, thick hydraulic hoses feeding it, and an anvil base below. Amber furnace light glows in the gap between hammer and anvil."),
    ],
    "futuro": [
        ("barrera_laser", 140, 420, "peligro",
         "A vertical security laser barrier: a small dark metal emitter bolted at the top and another at the bottom, and between them a hard bright cyan beam with a magenta core, crackling with tiny sparks where it meets each emitter. The beam has sharp pixel edges, not a soft glow."),
        ("plataforma_holografica", 320, 70, "plataforma",
         "A floating holographic platform: a flat rectangular slab of translucent cyan light with a bright solid edge, scan lines running across its surface, one corner glitched and offset sideways by a few pixels, and a small dark metal projector node at each end."),
        ("torre_servidor", 240, 440, "obstaculo",
         "A city server rack standing upright: a tall narrow black cabinet with rows of dark slots, cyan and magenta status lights blinking down one side, thick cables running out of its top and one cracked panel showing static behind the glass."),
        ("suelo_glitch", 260, 80, "plataforma",
         "A floor tile that is falling apart digitally: a slab of dark city pavement with neon edge lighting, its right half broken into offset horizontal bands of cyan and magenta as if the image were tearing, and loose pixel blocks drifting away from the break."),
        ("cartel_neon", 380, 240, "plataforma",
         "A fallen neon sign lying at an angle: a rectangular dark metal frame holding broken neon tubing, some tubes still lit in cyan and magenta and some dead and grey, with cracked glass, sparking wires at one corner and no readable letters of any kind, only abstract shapes."),
        ("mina_datos", 150, 150, "peligro",
         "A floating proximity mine made of compressed data: a dark angular polyhedron core with cyan seams, three thin rings of magenta light orbiting it at different angles, and small warning lights pulsing along its edges."),
    ],
    "hora_cero": [
        ("fragmento_reloj", 280, 130, "plataforma",
         "A floating slab of shattered reality: a flat chunk of black obsidian with gold gear teeth fused along its edges, its top surface flat enough to stand on, and a sliver of another world visible inside it like a window, showing a piece of castle wall. Cyan light bleeds from its cracks."),
        ("aguja_reloj", 520, 90, "plataforma",
         "A colossal clock hand lying flat, long enough to walk along: a tapered spear of dark iron and gold with an ornate counterweight at its blunt end and a fine point at the other, engraved with tiny gold numerals, wrapped in a faint cyan glow."),
        ("esquirla_tiempo", 140, 220, "peligro",
         "A shard of frozen time standing point-up out of the ground: a jagged spike of translucent cyan crystal with a darker violet core, hairline cracks running through it and a few smaller shards hanging suspended in the air beside it."),
        ("pilar_congelado", 220, 440, "obstaculo",
         "A pillar caught between three eras at once: its bottom third is medieval carved stone, its middle third is riveted industrial iron, and its top third is a black server column with cyan lights, the joins between them torn and bleeding cyan light, the whole thing wrapped in floating gold gear teeth."),
    ],
}

# ------------------------------------------------------------------ particulas
#
# Se pide una sola imagen con las motas sueltas y separadas, en vez de una
# rejilla ordenada: los generadores de imagen hacen fatal las rejillas exactas,
# y en cambio un reguero de manchas sueltas se recorta despues por programa sin
# problema, buscando cada mancha por separado.

PARTICULAS = {
    "medieval": "burning embers, flakes of grey ash, small cinders, specks of soot and tiny chips of grey stone. The embers are orange and gold and glow; the ash is pale grey; a couple of the flakes have a thin cyan frozen edge, as if time had caught them mid-fall.",
    "industrial": "orange sparks, hot slag droplets, flakes of black soot, small puffs of white steam and a few tiny brass gear teeth. The sparks and droplets glow hot; the soot is dead black; the steam puffs are solid pixel shapes, not soft clouds.",
    "futuro": "cyan data bits, magenta pixel blocks, small glitch fragments made of offset bands, thin cyan sparks and a few tiny hexagonal chips. Everything is hard-edged and digital, some pieces torn into two offset halves.",
    "hora_cero": "gold clock gear teeth, small gold cogs, splinters of cyan crystal, motes of gold dust and tiny chips of black obsidian with cyan cracks. Everything looks like a broken clock coming apart in the dark.",
}

PARTICULA_PROMPT = """{estilo}

SUBJECT: a scattering of {desc}

LAYOUT, and this part matters more than anything else: draw between 18 and 24 of these small pieces, spread out across the image, each one COMPLETELY SEPARATE from the others. No two pieces may touch, overlap, connect, or be joined by a trail, a streak or a glow. Leave a clear band of empty green between every piece and its neighbours, at least as wide as the pieces themselves. They are going to be cut out one by one by a program, so anything that touches becomes a single broken sprite.

Vary them: some big, some tiny, different shapes and different angles. Each piece must be between 8 and 40 pixels across. Do not arrange them in a grid, a line, a circle or any pattern; scatter them irregularly.

{fondo}

{negativos}"""


COMO_USAR_OBST = """OBSTACULOS: COMO USAR ESTOS PROMPTS
==================================

Son prompts de IMAGEN, uno por objeto. Se generan en la app de Gemini (o con
Nano Banana / Imagen), y NO son video: son sprites sueltos.

1. Genera la imagen con el prompt tal cual, sin adjuntar nada.

2. Guardala en esta misma carpeta, en la subcarpeta 'salida', con el mismo
   nombre que el prompt y en PNG:

       <era>/obstaculos/salida/roca_catapulta.png

3. El fondo verde se recorta despues por programa, igual que con los enemigos.
   Por eso el prompt insiste tanto en que el verde sea plano y en que no haya
   sombra proyectada sobre el fondo: una sombra se recorta como si fuese parte
   del objeto.

LOS TAMANOS NO SON DECORATIVOS
------------------------------
Cada prompt dice cuantos pixeles debe medir el objeto. La regla del proyecto es
100 px = 1 unidad de Unity, y Ekkar mide 240 px. Si el generador devuelve la
imagen mas grande o mas pequena no pasa nada, se reescala al montar; lo que
importa es la PROPORCION entre el objeto y el heroe, porque eso ya no se puede
arreglar despues.

EL ESTADO CONGELADO NO SE DIBUJA
--------------------------------
Cuando Ekkar detiene el tiempo (E), lo que estaba cayendo se vuelve solido y se
puede pisar. Eso NO hace falta dibujarlo: en Unity es el mismo sprite con un
tinte cian y la animacion parada. Con una version de cada objeto basta.
"""

COMO_USAR_PART = """PARTICULAS: COMO USAR ESTE PROMPT
=================================

Una sola imagen por era, con las motas sueltas y bien separadas. De ahi se
recortan una a una y se le dan al sistema de particulas, que ya existe en el
juego (EmberFall, SuspendedAsh, ParticleField) y es el que las mueve, las
inclina y las apaga.

1. Genera la imagen con el prompt tal cual.
2. Guardala como <era>/particulas/salida/particulas.png
3. Avisame y monto el recortador: separa cada mota, la deja como sprite suelto
   y las mete en la escena.

POR QUE NO ES UN VIDEO
----------------------
Porque las particulas no se repiten: cada mota cae a su ritmo, con su tamano y
su desfase, y eso lo decide el juego en tiempo real. Un video seria siempre el
mismo bucle y se notaria enseguida. Ademas asi cuestan cero creditos.

POR QUE NO ES UNA REJILLA
-------------------------
Porque los generadores de imagen hacen fatal las rejillas exactas. Un reguero
de manchas sueltas se recorta por programa sin problema, siempre que no se
toquen entre ellas. Por eso el prompt es tan pesado con la separacion.
"""


def main():
    total = 0
    indice = []

    for era, lista in OBSTACULOS.items():
        base = os.path.join(PROMPTS, era, "obstaculos")
        os.makedirs(os.path.join(base, "salida"), exist_ok=True)
        with open(os.path.join(base, "_COMO_USAR.txt"), "w", encoding="utf-8") as f:
            f.write(COMO_USAR_OBST)

        for nombre, ancho, alto, papel, desc in lista:
            with open(os.path.join(base, nombre + ".txt"), "w", encoding="utf-8") as f:
                f.write(build(nombre, desc, ancho, alto))
            indice.append((era, nombre, ancho, alto, papel))
            total += 1

        # particulas de la era
        pbase = os.path.join(PROMPTS, era, "particulas")
        os.makedirs(os.path.join(pbase, "salida"), exist_ok=True)
        with open(os.path.join(pbase, "_COMO_USAR.txt"), "w", encoding="utf-8") as f:
            f.write(COMO_USAR_PART)
        with open(os.path.join(pbase, "particulas.txt"), "w", encoding="utf-8") as f:
            f.write(PARTICULA_PROMPT.format(estilo=ESTILO, desc=PARTICULAS[era],
                                            fondo=FONDO, negativos=NEGATIVOS))
        total += 1
        print(f"  {era:11s} {len(lista)} obstaculos + 1 hoja de particulas")

    # indice para saber que hace cada pieza cuando toque montarla
    with open(os.path.join(PROMPTS, "_INDICE_OBSTACULOS.txt"), "w", encoding="utf-8") as f:
        f.write("QUE HACE CADA OBSTACULO EN EL JUEGO\n")
        f.write("===================================\n\n")
        f.write("plataforma = se puede pisar     obstaculo = bloquea el paso\n")
        f.write("peligro    = hace dano al tocarlo\n\n")
        ultimo = None
        for era, nombre, ancho, alto, papel in indice:
            if era != ultimo:
                f.write(f"\n{era.upper()}\n")
                ultimo = era
            f.write(f"  {nombre:24s} {ancho:4d}x{alto:4d} px   {papel}\n")

    print(f"\n{total} prompts escritos en Tools/prompts/<era>/obstaculos/ y /particulas/")
    print("indice -> Tools/prompts/_INDICE_OBSTACULOS.txt")


if __name__ == "__main__":
    main()
