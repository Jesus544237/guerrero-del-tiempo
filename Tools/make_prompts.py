# -*- coding: utf-8 -*-
"""Genera los prompts de las eras restantes. Fondo a recortar: verde #00FF00."""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "prompts")

STYLE = """Detailed 2D pixel art, 16-bit era style, hand-crafted look with crisp pixel edges and subtle dithering in the gradients. Cinematic dramatic lighting with strong readable silhouettes and clear depth separation between planes.

Colour palette, strictly: void #0A0515, deep purple #1A0A2E, mid purple #2D1B69, violet #7C3AED, cyan glow #06B6D4, bright cyan #22D3EE, gold #F59E0B, light gold #FBBF24, blood red #DC2626, cold white #E2E8F0.

Time is broken here: cyan light and floating golden clock gears leak into the scene, and small objects hang frozen in mid-air as if a moment had been paused."""

GREEN = """BACKGROUND: everything that is NOT the subject must be pure flat solid chroma green #00FF00, absolutely uniform, no gradient, no shadow, no glow spilling onto it. Chroma green must not appear anywhere on the subject itself, so it can be cut out cleanly."""

OPAQUE = """BACKGROUND: fully painted and opaque across the whole image. No transparency and no chroma green anywhere."""

NO = "Hard requirements: no characters, no creatures, no text, no letters, no numbers, no watermark, no signature, no user interface, no frame or border. Landscape 16:9, 2752x1536."

GROUND_RULE = """
COMPOSITION: fills the image from left to right. The TOP EDGE OF THE FLOOR MUST BE FLAT, STRAIGHT AND UNBROKEN across the entire width so the player can walk on it, sitting about one third of the way down the image. Everything below that line is solid material, fully painted down to the bottom edge with no holes. The left and right edges must match so the strip repeats seamlessly."""

ERAS = {
    "industrial": ("La Fundicion de las Horas", [
        ("00_cielo", "SUBJECT: the far sky of a factory city at night, choked with smog. A sickly amber haze glowing from below, torn purple-brown smoke banks, a starless sky and cold cyan cracks splitting the clouds. A few enormous golden gears turn slowly among the smoke, very far away.\n\nCOMPOSITION: pure background plate, painted from the top edge down to at least three quarters of the height. Nothing in sharp focus.", True),
        ("01_horizonte", "SUBJECT: the distant silhouette of an endless industrial skyline: blast furnaces, cooling towers, chimney stacks, gantry cranes and riveted iron bridges, flat and dark. Amber furnace light in the vents. Smoke rises from the stacks and stops dead halfway up, frozen.\n\nCOMPOSITION: a LOW HORIZONTAL BAND of city filling only the bottom third; the entire upper area is empty.", False),
        ("02_fabrica", "SUBJECT: the mid-ground, the great foundry caught mid-explosion and held there forever. A riveted iron hall with a broken roof, huge pressure pipes, catwalks, and a colossal escapement wheel embedded in its front wall. Sparks, bolts and shards of hot metal hang MOTIONLESS, each in a thin cyan halo. Molten metal pours from a ladle and freezes mid-fall like a golden ribbon.\n\nCOMPOSITION: the structure occupies the middle band, its base simple and dark so a floor layer can overlap it.", False),
        ("03_suelo", "SUBJECT: a horizontal floor strip seen straight from the side, flat orthographic view with NO perspective. Riveted steel walkway plates over cracked concrete, rail tracks, scattered bolts, spilled coal and a spent gear half sunk in the floor. Thin cyan light seeps from between the plates." + GROUND_RULE, True),
        ("04_frente", "SUBJECT: a sheet of separate foreground props in a single row with clear empty space between them, all standing on the same invisible ground line: a bent iron lamppost, a stack of steel drums, a broken valve wheel, a toppled minecart, a coil of heavy chain, a cracked pipe venting frozen steam, a pile of scrap plate, a rusted gear taller than a man. Every prop very dark, almost a silhouette, with a thin cold cyan rim light down its left side.", False),
    ]),
    "futuro": ("Neon Sin Manana", [
        ("00_cielo", "SUBJECT: the far sky over a cyberpunk megacity at night. Deep violet-black rain clouds lit from below by neon, streaks of cyan and magenta bleeding upward, cold stars, and a thin ring of golden gears drifting like orbital debris.\n\nCOMPOSITION: pure background plate, painted from the top edge down to at least three quarters of the height. Nothing in sharp focus.", True),
        ("01_horizonte", "SUBJECT: the distant skyline of a vast neon city in flat dark silhouette: layered skyscrapers, antenna spires, holographic billboards, elevated maglev lines, cyan and magenta window lights. A few towers glitch, their upper halves offset sideways as if the image had skipped a frame.\n\nCOMPOSITION: a LOW HORIZONTAL BAND of city filling only the bottom third; the entire upper area is empty.", False),
        ("02_servidor", "SUBJECT: the mid-ground, a colossal server cathedral with its face split open. Towers of black server racks with cyan status lights, hanging cable bundles, and a giant cracked holographic clock face projected across the structure. Shards of broken hologram and sparks hang MOTIONLESS, each in a thin cyan halo. Golden gears are fused into the machinery.\n\nCOMPOSITION: the structure occupies the middle band, its base simple and dark so a floor layer can overlap it.", False),
        ("03_suelo", "SUBJECT: a horizontal floor strip seen straight from the side, flat orthographic view with NO perspective. Wet dark ferroconcrete with embedded light strips, grating panels, cracked conduits and shallow puddles reflecting cyan and magenta. Thin cyan light seeps from the cracks." + GROUND_RULE, True),
        ("04_frente", "SUBJECT: a sheet of separate foreground props in a single row with clear empty space between them, all on the same invisible ground line: a shattered neon sign, a crashed delivery drone nose-down, a stack of cargo crates, a bent street pole trailing cables, a burst coolant pipe venting frozen vapour, a broken holo-billboard frame, a toppled vending machine, a security barrier. Every prop very dark, almost a silhouette, with a thin cyan rim light down its left side.", False),
    ]),
    "hora_cero": ("La Hora Cero", [
        ("00_vacio", "SUBJECT: the void between seconds. Absolute black emptiness with no ground and no sky, crossed by slow concentric rings of cyan light like ripples on still water, and enormous golden gears turning silently at different depths. One vast cracked clock face floats far behind everything, its hands missing.\n\nCOMPOSITION: pure background plate, painted edge to edge, darkest in the centre so a boss fight reads clearly on top of it.", True),
        ("01_ecos", "SUBJECT: ghostly echoes of three eras bleeding into the void: on the left a fragment of gothic castle wall, in the centre a piece of iron foundry catwalk, on the right a shard of neon skyscraper. Each fragment floats free with broken edges, translucent and outlined in cyan, as if the same place existed in three times at once.\n\nCOMPOSITION: three separate floating masses with clear empty space between them, in the middle band of the image.", False),
        ("02_trono", "SUBJECT: the throne of the Lord of Time: a colossal broken clock mechanism suspended in nothing. Concentric golden gear rings the size of buildings, a shattered escapement, chains of frozen hourglasses, and at its heart a hollow socket where the Great Clock's core should sit, pouring cyan light.\n\nCOMPOSITION: one huge centred structure with empty space all around it.", False),
        ("03_suelo", "SUBJECT: a horizontal platform strip seen straight from the side, flat orthographic view with NO perspective. A floor of interlocking golden gear teeth and black obsidian plates veined with cyan light, its underside crumbling into floating fragments." + GROUND_RULE, True),
        ("04_frente", "SUBJECT: a sheet of separate foreground props in a single row with clear empty space between them: a giant cracked hourglass, a broken sword frozen mid-fall, a toppled column of gear teeth, a hanging chain of pocket watches, a shard of mirror showing another era, a ring of suspended gears, a banner turned to ash, a stopped pendulum. Every prop very dark, almost a silhouette, with a thin cyan rim light down its left side.", False),
    ]),
}


def main():
    for folder, (name, layers) in ERAS.items():
        d = os.path.join(OUT, folder)
        os.makedirs(d, exist_ok=True)
        for fn, subject, opaque in layers:
            bg = OPAQUE if opaque else GREEN
            txt = f'{STYLE}\n\nLEVEL: "{name}".\n\n{subject}\n\n{bg}\n\n{NO}\n'
            with open(os.path.join(d, fn + ".txt"), "w", encoding="utf-8") as f:
                f.write(txt)
        print(f"  {folder:12s} {len(layers)} prompts")


if __name__ == "__main__":
    main()
