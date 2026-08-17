# -*- coding: utf-8 -*-
"""
Genera los prompts de VIDEO de todos los enemigos, mini-jefes y del jefe final.

Los prompts estan escritos para Gemini Video / Flow: camara fija, fondo verde
croma en todo el clip, sin desenfoque de movimiento y con el ciclo repetido
varias veces, que es lo que hace falta para poder sacar los fotogramas y
montarlos como sprite sheet.

Uso:
    python Tools/make_enemy_video_prompts.py
"""

import os
import shutil

HERE = os.path.dirname(os.path.abspath(__file__))
PROMPTS = os.path.join(HERE, "prompts")

# --------------------------------------------------------------- bloques fijos

STYLE = """RENDERING: 2D pixel art, 16-bit era style, crisp hard pixel edges and subtle dithering. NO motion blur, NO anti-aliasing smear, NO film grain, NO lens flare, NO depth of field, NO cinematic colour grading. Every single frame must look like a hand-drawn sprite frame that could be cut straight out of a sprite sheet.

Colour palette, strictly: void #0A0515, deep purple #1A0A2E, mid purple #2D1B69, violet #7C3AED, cyan glow #06B6D4, bright cyan #22D3EE, gold #F59E0B, light gold #FBBF24, blood red #DC2626, cold white #E2E8F0."""

CAMERA = """CAMERA: absolutely locked off. No panning, no zooming, no tilting, no camera shake, no parallax. The camera does not move for a single frame of the clip."""

PLACEMENT = """SUBJECT PLACEMENT: one single character, seen from a strict SIDE VIEW facing right, standing on an invisible ground line. It stays centred in the LEFT TWO THIRDS of the frame at a constant size for the whole clip. It animates in place, like on a treadmill: it must never drift across the frame, never change scale, never leave the frame and never turn to face the camera.

The BOTTOM-RIGHT CORNER of the frame must stay completely empty green, with no part of the character, no effect and no glow reaching into it."""

BACKGROUND = """BACKGROUND: pure flat solid chroma green #00FF00 filling the entire frame for the entire duration, absolutely uniform, no gradient, no shadow, no vignette, no light spill onto it, no ground, no floor, no scenery, no props. The green must be the exact same shade in every frame. Chroma green must never appear anywhere on the character itself or in its effects."""

NEGATIVES = """Hard requirements: only one character, no other creatures, no text, no letters, no numbers, no user interface, no health bars, no logos, no frame, no border, no background elements of any kind."""


def loop_block(cycles=3):
    return f"""TIMING: the animation is a seamless loop. The last frame must match the first frame exactly. Play the complete cycle {cycles} times over the clip, at a steady even speed, with no pauses and no speed ramps."""


def oneshot_block(times=2):
    return f"""TIMING: this is a one-shot action, not a loop. The character starts in its neutral standing pose, performs the action, and returns to the neutral pose. Perform the whole action {times} times in a row, with a short still beat in the neutral pose between repetitions, at a steady even speed with no speed ramps."""


def single_block():
    return """TIMING: this is a one-shot cinematic action, not a loop. It plays through exactly ONCE over the whole clip, at a steady even speed with no speed ramps and no repetition. Hold the final pose completely still for the last few frames so the end of the animation can be read cleanly."""


def build(subject, action, timing, extra=""):
    parts = [STYLE, "", f"SUBJECT: {subject}", "", f"ANIMATION: {action}", "",
             CAMERA, "", PLACEMENT, "", BACKGROUND, "", timing]
    if extra:
        parts += ["", extra]
    parts += ["", NEGATIVES]
    return "\n".join(parts) + "\n"


REF = "Look carefully at the attached reference image of the hero Ekkar and match its art style, pixel density, outline treatment, shading and body proportions exactly, so this character looks like it belongs in the same game."

# ------------------------------------------------------------------ enemigos

ANIMS = [
    ("01_idle", "idle", True),
    ("02_caminar", "walk", True),
    ("03_ataque", "attack", False),
    ("04_dano", "hurt", False),
    ("05_muerte", "death", False),
]

ENEMIES = {
    "medieval": [
        ("soldado_hueso",
         f"{REF} An undead medieval skeleton soldier: bleached bone in dented rusted iron plate over rotten leather, a battered kettle helm with a torn blood-red plume, a chipped shortsword and a splintered round shield. Cold cyan light burns in its eye sockets and thin cyan vapour trails from its joints. One small gold clock gear is fused into its ribcage. Slightly shorter and leaner than the hero.",
         {
             "idle": "it stands in a combat-ready crouch, breathing shallowly: ribcage and shoulders rise and settle, the plume and the cyan vapour drift, the sword tip sways a little. Very small movement.",
             "walk": "a slow, heavy, dragging stride. Contact, down, passing, up, then the same for the other leg. The shield arm swings opposite the leading leg and the plume trails behind.",
             "attack": "it winds the shortsword back over its shoulder, steps in and slashes down and across, holding the full extension for one beat with a bright cyan slash arc, then pulls back to guard. The slash arc appears only at full extension.",
             "hurt": "it snaps backwards, skull thrown back, shield arm flung wide, bone chips and cyan sparks bursting from the impact point, then straightens up again. The whole body flashes brighter white for one frame at the moment of impact.",
             "death": "the cyan light drains out of the eye sockets, the knees buckle, it collapses forward and comes apart into loose bones and a puff of cyan vapour, ending as a scatter of bones lying still on the ground line.",
         }),
        ("espectro_asedio",
         f"{REF} The ghost of a medieval siege soldier: a hollow-faced spirit in a tattered hooded surcoat, translucent cyan and violet, its lower body dissolving into a drifting ribbon of vapour instead of legs. Long thin clawed hands. A tiny suspended hourglass wrapped in gold gear teeth hangs inside its chest. About the hero's height but much narrower.",
         {
             "idle": "it hovers in place, bobbing gently up and down, the tattered surcoat and the vapour ribbon below it rippling slowly. Thin cyan afterimages flicker behind it as if the same instant kept repeating.",
             "walk": "it glides forward in place, leaning into the movement, the vapour ribbon streaming back and the afterimages stretching into a short trail behind it.",
             "attack": "it rears back, opens its hollow mouth wide, and lunges forward with both clawed hands extended, leaving a hard cyan afterimage of itself behind at the start of the lunge, then drifts back to its hovering pose.",
             "hurt": "it recoils and briefly loses cohesion, its whole body scattering into vapour and cyan pixels for a few frames before pulling itself back together.",
             "death": "the hourglass in its chest cracks, the spirit unravels from the bottom upward into ribbons of cyan vapour that dissipate, ending with an empty frame of clean background.",
         }),
        ("ballestero_ceniciento",
         f"{REF} An undead crossbowman: a skeletal archer in a burnt hooded cloak over rusted mail, carrying a heavy iron crossbow with a gold gear wound into its winch. Cyan fire in the eye sockets, ash falling constantly from the hem of the cloak. Same height as the hero but hunched.",
         {
             "idle": "it stands hunched with the crossbow lowered, scanning left and right in small movements, the cloak hem stirring and ash drifting down from it.",
             "walk": "a cautious sidestepping shuffle, crossbow held ready across the chest, cloak swaying.",
             "attack": "it raises the crossbow to the shoulder, holds aim for one beat with a cyan glint at the tip, fires with a sharp recoil and a burst of cyan sparks, then cranks the winch to reload and lowers back to the ready pose.",
             "hurt": "it jerks backwards, the crossbow swinging wide, ash and bone fragments bursting outward, then recovers its stance.",
             "death": "the crossbow drops from its hands, the body folds inward and crumbles into a pile of ash and bone with a last puff of cyan light.",
         }),
    ],
    "industrial": [
        ("automata_fundicion",
         f"{REF} A foundry automaton: a squat riveted iron worker with a furnace glowing amber behind its chest grille, mismatched hydraulic arms ending in a clamp and a hammer, a cracked glass dome for a head with a single cyan lens. Steam vents from its shoulders. Slightly shorter and much wider than the hero.",
         {
             "idle": "it stands with the furnace pulsing amber, shoulders venting small puffs of steam on a slow rhythm, the cyan lens sweeping side to side, hydraulics twitching.",
             "walk": "a heavy stomping march, one foot slamming down hard enough to make the whole chassis rock, steam venting on each step.",
             "attack": "it winds the hammer arm back with a hiss of hydraulics, holds for one beat as the furnace flares bright, then swings the hammer down hard, releasing a burst of amber sparks and steam at the impact, and resets to its stance.",
             "hurt": "it staggers backwards, the glass dome cracking further, steam blasting from a ruptured shoulder pipe, the furnace flickering, then it rights itself.",
             "death": "the furnace flares white, the chest grille blows open, and the automaton collapses joint by joint into a heap of scrap plate and loose bolts, venting a final long jet of steam.",
         }),
        ("dron_vapor",
         f"{REF} A steam drone: a brass sphere the size of a barrel held aloft by a spinning gold gear-rotor, trailing a torn hose that leaks white steam, with a riveted iron faceplate and a single amber eye. Small grabbing claws underneath. About two thirds the hero's height.",
         {
             "idle": "it hovers in place, the gear-rotor spinning steadily above it, bobbing slightly, steam puffing from the trailing hose, the amber eye pulsing.",
             "walk": "it drifts forward in place, tilting nose-down into the movement, rotor spinning faster and the hose streaming steam behind it.",
             "attack": "it tilts back, the amber eye flares bright, and it releases a short downward burst of scalding steam and glowing sparks from its underside, then levels out again.",
             "hurt": "it spins off balance, rotor stuttering, sparks spitting from the faceplate, then stabilises.",
             "death": "the rotor seizes and snaps, the sphere drops out of the air, bursts open in a cloud of steam and gold gear teeth, and the debris settles.",
         }),
        ("capataz_oxidado",
         f"{REF} A rusted foreman: a tall gaunt automaton in a long soot-stained coat over an iron frame, one arm replaced with a riveted molten-metal launcher that glows amber inside, a cracked pressure gauge set into its chest, and a wide-brimmed iron hat. Taller than the hero but thin.",
         {
             "idle": "it stands upright and still, coat stirring, the pressure gauge needle twitching, the launcher arm glowing faintly amber and cooling, small embers dripping from its muzzle.",
             "walk": "a stiff, formal stride, coat swinging, the heavy launcher arm counterweighting each step.",
             "attack": "it plants its feet, raises the launcher arm, the muzzle glowing white-hot for a beat, then fires a bolt of molten metal with a hard recoil and a shower of amber sparks, and lowers the arm as it vents steam.",
             "hurt": "it doubles over, the pressure gauge shattering and venting a jet of steam from its chest, then straightens up stiffly.",
             "death": "the gauge blows out, the coat catches amber fire, the frame buckles at the knees and folds forward into a smoking heap of iron.",
         }),
    ],
    "futuro": [
        ("dron_centinela",
         f"{REF} A sentinel drone: a sleek black wedge-shaped hovering machine with cyan running lights along its edges, a magenta scanner eye at the front, and two small thruster vents underneath that glow cyan. Occasionally its whole body glitches, offsetting sideways by a few pixels. About half the hero's height.",
         {
             "idle": "it hovers in place, bobbing slightly, running lights pulsing in sequence, the magenta scanner eye sweeping left and right. Every cycle its body glitches once, tearing sideways by a few pixels for a single frame before snapping back.",
             "walk": "it drifts forward in place, nose tipped down, thruster vents flaring cyan, leaving a short trail of glitched afterimages behind it.",
             "attack": "the scanner eye locks and flares magenta, the drone recoils backwards as it fires a thin horizontal cyan beam, holding the beam for a beat before it snaps off and the drone levels out.",
             "hurt": "it spins sideways, lights flickering red, its body tearing into glitched horizontal bands before reassembling.",
             "death": "the lights die one by one, the body glitches violently apart into scattered cyan and magenta pixel blocks that fall and fade out.",
         }),
        ("corredor_datos",
         f"{REF} A data runner: a lean humanoid figure made of compressed light and broken code, its body a dark silhouette filled with scrolling cyan characters, wearing a torn magenta jacket that trails like a data stream. Its head is a featureless cracked screen showing static. Same height as the hero but thinner and faster-looking.",
         {
             "idle": "it stands loose and twitchy, weight shifting foot to foot, the code inside its body scrolling upward, the screen-head flickering with static, the jacket rippling like a bad signal.",
             "walk": "a fast sprint in place, leaning far forward, legs blurring into stepped afterimages rather than smooth blur, the jacket streaming straight back and the body leaving glitched copies behind.",
             "attack": "it snaps forward into a dashing lunge, its body stretching into three hard-edged afterimages along the path, striking with an open hand wreathed in cyan data, then reassembling back at its starting pose.",
             "hurt": "its body corrupts violently, splitting into offset horizontal bands of magenta and cyan, the screen-head bursting into static, before it snaps back together.",
             "death": "the scrolling code drains out of its body from the head down, the silhouette collapses into a falling column of cyan characters that scatter and vanish.",
         }),
        ("torreta_holografica",
         f"{REF} A holographic turret: a heavy hexagonal base bolted to the ground with a floating projector head hovering just above it, ringed by three rotating gold gear-plates. The head is a cracked cyan hologram of an eye. Magenta warning lights on the base. Two thirds the hero's height, wide and squat.",
         {
             "idle": "the base sits still while the projector head hovers and slowly rotates, the gear-plates turning around it, the holographic eye blinking and flickering with scan lines.",
             "walk": "it does not walk. Instead the projector head rises higher off the base and settles back down in a slow hovering pulse, gear-plates spinning up and slowing again.",
             "attack": "the gear-plates spin fast and lock, the holographic eye contracts to a bright point, then the turret fires a short wide cone of cyan light forward, holding it for a beat before the head sags and the plates unlock.",
             "hurt": "the hologram tears into flickering bands and the base sparks magenta, the gear-plates wobbling out of alignment before recovering.",
             "death": "the projector head implodes into a single point and bursts, the gear-plates fall away and clatter, and the base cracks open venting cyan light before going dark.",
         }),
    ],
    "hora_cero": [
        ("eco_de_ekkar",
         f"{REF} A time echo of the hero himself: the exact same silhouette, armour and cape as the attached reference, but rendered as a hollow negative — the armour is deep violet-black, the cape is dark cyan instead of red, and the whole figure is outlined in bright cyan with a faint double image trailing behind it. Its face is a blank cyan glow with no features. Exactly the hero's height.",
         {
             "idle": "it stands in the hero's own ready stance, breathing, cape drifting, its trailing double image lagging a fraction of a second behind every movement.",
             "walk": "it runs in place with the hero's exact running gait, the lagging double image following one beat behind, cyan outline flaring on each footfall.",
             "attack": "it performs the hero's own horizontal sword slash, but the cyan afterimage repeats the same slash a moment later, so the attack appears to happen twice.",
             "hurt": "it flickers out of existence for a few frames, the trailing double image left standing alone, before snapping back into place.",
             "death": "the echo and its double image drift apart, stretch, and unravel into ribbons of cyan light that spiral inward to a point and vanish.",
         }),
        ("fragmento_animado",
         f"{REF} An animated fragment: a floating chunk of shattered reality about the size of a shield, made of interlocking gold gear teeth and black obsidian, with a sliver of a different era visible inside it like a window (a piece of castle wall, of iron catwalk, of neon sign) that keeps changing. Cyan light bleeds from its cracks. About half the hero's height.",
         {
             "idle": "it hovers and rotates slowly on the spot, the gear teeth around its edge turning, the era visible inside its window shifting between medieval stone, industrial iron and neon city.",
             "walk": "it drifts forward in place, tumbling end over end at a steady rate, trailing small cyan sparks.",
             "attack": "it stops rotating, pulls back, the window inside flaring bright, then slams forward with its gear-toothed edge leading, releasing a burst of cyan shards, and drifts back.",
             "hurt": "it cracks further, pieces breaking loose and orbiting it, cyan light pouring from the new fractures before it pulls itself back together.",
             "death": "it splits along every crack at once, the window inside going dark, and bursts into a slow expanding cloud of gear teeth and obsidian shards that fade out.",
         }),
    ],
}

# ------------------------------------------------------------------ mini-jefes

MIDBOSS = {
    "medieval": ("caballero_ceniciento",
        f"{REF} 'The Ashen Knight', the champion of the fallen kingdom and a dark mirror of the hero: the SAME full-plate silhouette, but burnt black and grey, every plate cracked open with cold cyan light glowing from inside like a furnace of frozen time. Closed faceless helm with two slit eyes of cyan fire. A charred blood-red cape whose lower edge crumbles into ash that hangs motionless in the air instead of falling. He grips a two-handed greatsword broken halfway, its missing fragments floating beside the blade, ringed with gold gear teeth. Roughly one and a half times the hero's height and much broader.",
        {
            "idle": "he stands heavy and still, greatsword point resting on the ground, chest rising slowly, cyan light pulsing inside the cracks of his armour, the floating blade fragments drifting around the broken sword and the cape ash hanging suspended.",
            "walk": "a slow, deliberate, ground-shaking advance, dragging the greatsword tip along the ground so it throws up a line of cyan sparks, cape swinging heavily.",
            "attack": "he raises the greatsword high overhead with both hands, the floating fragments snapping back into place to complete the blade, holds for a long beat as the cyan light in his armour flares white, then brings it down in a devastating overhead cleave that ends with the blade embedded in the ground; the fragments break away again as he pulls it free.",
            "hurt": "he takes one step back, armour plates flaring bright cyan at the impact, ash and cinders bursting from the cape, then plants his feet again without lowering his guard.",
            "death": "he sinks to one knee, the greatsword falling from his hands and shattering into fragments that hang frozen in the air, the cyan light draining out of every crack in his armour until he goes dark and still, and finally the whole figure crumbles into a pile of ash.",
            "especial": "he plants the greatsword into the ground with both hands and drives cyan energy through it: a ring of frozen cyan blades erupts out of the ground around him in an expanding circle, hangs suspended for a beat, then shatters as he pulls the sword back out.",
        }),
    "industrial": ("el_gran_yunque",
        f"{REF} 'The Great Anvil', a colossal pile-driver automaton: a bulky riveted iron chassis on two short tracked legs, with a single enormous piston hammer for a right arm that reaches almost to the ground, a furnace core glowing amber behind a barred chest, and a squat armoured head with three amber lenses. Chains and pressure hoses hang from its frame. Twice the hero's width and half again his height.",
        {
            "idle": "it idles with the furnace core pulsing amber, pressure hoses flexing, steam venting rhythmically from its back, the three lenses blinking out of sync, the piston hammer hanging heavy and swaying slightly.",
            "walk": "it grinds forward on its tracks, the whole chassis rocking side to side, the piston arm dragging and striking the ground on every second step with a burst of sparks.",
            "attack": "the piston arm retracts with a rising hiss as the furnace flares white-hot, holds compressed for a beat, then fires downward with enormous force, the hammer slamming into the ground and sending a shockwave of amber sparks and steam outward before slowly retracting.",
            "hurt": "the chassis rocks backwards, hoses whipping loose and venting steam, one lens shattering, the furnace flickering before stabilising.",
            "death": "the furnace core goes critical and blinds white, chains snap, the piston arm tears free and crashes down, and the chassis buckles and collapses into a smoking mountain of scrap iron.",
            "especial": "it anchors both tracks, opens its chest to expose the furnace, and sprays a wide arc of molten metal droplets forward that freeze in mid-air halfway through the arc, hanging suspended in cyan halos before dropping all at once.",
        }),
    "futuro": ("nemesis_digital",
        f"{REF} 'Digital Nemesis', a corrupted digital copy of the hero: the exact same armour and cape silhouette as the attached reference, but rebuilt out of neon wireframe and broken polygons, the armour rendered in magenta and cyan gradients with visible triangular facets, the cape a sheet of scrolling code. Its face is the hero's own face rendered as a flickering low-resolution hologram with the eyes replaced by magenta scan lines. Same height as the hero exactly.",
        {
            "idle": "it stands in the hero's ready stance, its polygons subtly breathing and reshaping, the code-cape rippling, the whole figure tearing into offset horizontal bands for a single frame every cycle.",
            "walk": "it runs in place with the hero's exact gait, but its trailing leg and arm lag behind as stretched wireframe smears, and its body periodically duplicates into two offset copies before resolving.",
            "attack": "it raises a wireframe sword that assembles out of floating polygons mid-swing, slashes horizontally leaving a magenta and cyan double arc, then the sword disassembles back into scattered polygons.",
            "hurt": "its body shatters into floating polygons that hang apart for a moment, the hologram face collapsing into static, before all the pieces snap violently back together.",
            "death": "its polygons detach one by one and drift outward, the code-cape unravelling into falling characters, the hologram face flickering through the hero's own expression once before going black, and the last wireframe outline collapsing to a point.",
            "especial": "it splits into three identical offset copies of itself, magenta, cyan and white, which fan out and strike in sequence one after another, then collapse back into a single body.",
        }),
}

# ------------------------------------------------------------------ jefe final
#
# El jefe NO se describe de cero. Sus dos imagenes de referencia ya estan
# pintadas y son las buenas, asi que cada clip se genera con la imagen como
# primer fotograma (image-to-video). Un prompt de texto largo le deja a Gemini
# sitio para reinventarlo, y por eso salia con un diseno distinto en cada clip.
# Aqui lo unico que tiene que decidir el modelo es el movimiento.

REF_LIMPIO = "senor_del_tiempo_de_pie_verde_sin_portal.jpg"
REF_FASE2 = "senor_del_tiempo_fase2_ASPECTO_fondo_verde_1.jpg"
REF_PORTAL = "senor_del_tiempo_de_pie_verde_portal.jpg"      # esta falta, ver PASO 0

BOSS_ANCLA = """IMAGE TO VIDEO. The attached image '{ref}' is the FIRST FRAME of this clip, and the character in it is final and correct. Animate exactly that image. Do not redraw him, do not redesign him, do not restyle him, do not clean him up, do not change his proportions, his armour, his helm, his face, his colours or the number of his limbs. Where a detail is unclear, copy it from the image instead of inventing it."""

BOSS_CUERPO = """THE CHARACTER, so that he is not reinvented: one single humanoid figure in heavy dark blue-steel plate armour with gold trim, floating upright and facing the camera. He has TWO ARMS and TWO LEGS and nothing else: two long segmented armoured arms ending in clawed gauntlets, and two armoured legs hanging slightly bent, because he never touches the ground. Horned helm, and under it a bone-pale grinning face with a wide fanged mouth and two burning cyan eyes. Huge shoulder plates. Cyan light burns in the seams of his armour and across his chest.

Two things move around him and NEITHER of them is a limb:
  - thin dark tendrils of his own stretched flesh, growing out of his head, his shoulders, his back and his legs. They pull out longer and curl back on themselves, as if he were slowly being drawn out of shape. He is coming apart in time, and this is how it shows.
  - wide flat golden ribbons with a pale cyan edge, looping around him through the air, together with gold clock gears of several sizes turning in orbit.

THE TENDRILS ARE ALREADY DRAWN IN THE ATTACHED IMAGE AND THEY ARE ALL THE TENDRILS HE HAS. Move them, curl them, let them stretch and shorten, but never add a single new one. Do not multiply them, do not branch them, do not grow them out of new places, do not let them fill the frame, do not turn them into a mass, a nest or a swarm. Count them in the attached image: there must be the same number in every frame of the clip.

He never grows extra arms. The tendrils and the ribbons never become arms, wings, hair or creatures with a life of their own."""

BOSS_FASE2 = """PHASE 2 LOOK: the same body, the same silhouette, the same two arms and two legs, the same tendrils and the same golden ribbons. What changes is this: his armour has turned charcoal black with glowing gold engraving in every plate, his eyes and his mouth burn ORANGE instead of cyan, a wide band of orange and white fire burns across his chest, and a ball of violet lightning crackles around each of his hands. Keep the fire exactly where it is in the attached image: it must never grow big enough to cover his armour, his face or his body."""

BOSS_RENDER = """RENDERING: keep the exact pixel art of the attached image: same pixel size, same hard edges, same outlines, same palette. NO motion blur, NO smearing, NO anti-aliasing wash, NO depth of field, NO film grain, NO relighting, NO cinematic colour grading, NO change of art style. Every single frame must look like a hand-drawn sprite frame that could be cut straight out of a sprite sheet."""

BOSS_CAMERA = """CAMERA: absolutely locked off. No panning, no zooming, no tilting, no camera shake, no parallax. The camera does not move for a single frame of the clip."""

BOSS_PLACEMENT = """SUBJECT PLACEMENT: exactly one character, seen from the FRONT, floating in the middle of the frame at the same size and in the same place as in the attached image. He animates in place: he must never drift across the frame, never change scale, never turn around and never leave the frame.

The BOTTOM-RIGHT CORNER of the frame must stay completely empty green, with no part of the character, no tendril, no ribbon, no gear and no glow reaching into it."""

BOSS_BACKGROUND = """BACKGROUND: the flat chroma green #00FF00 of the attached image stays exactly as it is, filling the whole frame behind him, absolutely uniform and the exact same shade in every frame. No gradient, no shadow, no vignette, no light spill onto the green, no ground, no floor, no scenery, no props. Chroma green must never appear on the character himself, on his tendrils, on his ribbons or in his effects."""

BOSS_NEGATIVES = """Hard requirements: only this one character, never a second copy of him, no extra arms, no extra legs, NO EXTRA TENDRILS AND NO NEW TENDRILS OF ANY KIND, no other creatures, no redesign, no text, no letters, no numbers, no user interface, no health bars, no logos, no frame, no border, no background elements of any kind."""


def build_boss(ref, action, timing, fase=1, extra=""):
    cuerpo = BOSS_CUERPO if fase != 2 else BOSS_CUERPO + "\n\n" + BOSS_FASE2
    parts = [BOSS_ANCLA.format(ref=ref), "", cuerpo, "", f"ANIMATION: {action}", "",
             BOSS_RENDER, "", BOSS_CAMERA, "", BOSS_PLACEMENT, "", BOSS_BACKGROUND, "", timing]
    if extra:
        parts += ["", extra]
    parts += ["", BOSS_NEGATIVES]
    return "\n".join(parts) + "\n"


# (archivo, imagen de referencia, fase, movimiento, tipo, nota extra)
BOSS = [
    ("00_portal_aparicion", REF_PORTAL, 1,
     "In the attached image he is ALREADY out and standing in front of the portal: his feet are clear of it and no part of him is inside it. He does not step out, he does not climb out, he does not pull himself out of it. He simply holds his stance, drifts forward a hand's width and settles, while BEHIND him the portal closes on its own: its swirl slows down, it shrinks inward towards its own centre and fades out, until there is only flat green where it was. He ends standing in his neutral pose with nothing behind him.",
     "single",
     f"TWO IMAGES: the first attached image is the FIRST FRAME. The second attached image '{REF_LIMPIO}' is the LAST FRAME: the same character, standing the same way, in the same place and at the same size, with no portal at all. Get from the first to the second and hold that last pose still for the final frames.\n\nThe portal must NOT shatter, NOT break into shards, NOT explode and NOT throw debris or fragments: it closes in on itself and fades out, and it is completely gone by the last third of the clip, with no glow and no leftover energy. Nothing of him ever passes through the portal: his legs, his arms and his tendrils never go into it, never come out of it and never stretch through it. His legs stay where they are and keep their shape at all times."),

    ("01_idle_fase1", REF_LIMPIO, 1,
     "He floats in place and breathes: his chest rises and settles, his shoulders drift, his two arms sway very slightly, and his head tilts a fraction. The dark tendrils grow out a little longer and curl back in again, over and over, so he looks like he is being slowly stretched and pulled back. The golden ribbons loop lazily around him, the gold gears keep turning, and the cyan light in his armour and in his eyes pulses. Small movement, heavy and unhurried.",
     "loop", ""),

    ("02_ataque_pendulo", REF_LIMPIO, 1,
     "He raises both arms above his head and a colossal pendulum blade of gold and cyan light forms hanging over him. He swings it down and across in one wide arc in front of him, holds at the end of the arc for a beat while his tendrils whip out straight behind him, then the pendulum dissolves and he settles back into his floating pose.",
     "oneshot", ""),

    ("03_ataque_agujas", REF_LIMPIO, 1,
     "He spreads both arms wide and a ring of long clock-hand spears of gold and cyan light forms in the air around him, hanging dead still. He snaps both arms forward and every spear fires forward at once leaving cyan trails, then he settles back into his floating pose.",
     "oneshot", ""),

    ("04_ataque_detener_tiempo", REF_LIMPIO, 1,
     "He brings both hands together in front of his chest and a sphere of pale cyan light collapses inward between them, then bursts outward as an expanding ring that washes over the whole frame. As the ring passes, everything about him freezes completely still except his eyes, which keep burning, and his tendrils, which hang frozen mid-stretch. He holds frozen for a full second, then motion returns and he resumes floating.",
     "oneshot", ""),

    ("05_ataque_eco", REF_LIMPIO, 1,
     "He sweeps one arm outward and three flat translucent cyan ghosts rise out of the bottom of the frame in front of him: a skeleton soldier, a foundry automaton and a small hovering drone, each a plain cyan silhouette. They hold for a beat, lunge forward once together, then dissolve into vapour as he settles back into his floating pose.",
     "oneshot", ""),

    ("06_dano", REF_LIMPIO, 1,
     "He snaps backwards, both arms flung wide, the cyan fire in his eyes flaring white for one frame, his tendrils yanked taut and whipping, gold gears knocked out of their orbit and spinning loose, and cracks of white light opening briefly across his armour plates. Then he pulls himself upright again into his floating pose.",
     "oneshot", ""),

    ("07_transformacion_fase2", REF_LIMPIO, 1,
     "He throws his head back and both arms outward. The cyan light drains out of his armour and, plate by plate, the armour turns charcoal black while gold engraving lights up along every edge. The fire in his eyes and his mouth turns from cyan to orange, a wide band of orange and white fire tears open across his chest, and a ball of violet lightning ignites around each hand. His tendrils stretch out further than before and stay long. He ends standing in his phase 2 form.",
     "single",
     f"TWO IMAGES: the first attached image is the FIRST FRAME. The second attached image '{REF_FASE2}' is the LAST FRAME. Get from the first to the second and hold that last look completely still for the final frames.\n\nThis is a transformation, so he deliberately CHANGES between the start and the end, but ONLY on the surface: his body, his pose, his proportions, his silhouette, his two arms, his two legs and the number of his tendrils are the same in the first frame and in the last. What changes is the colour of his armour, the colour of his eyes and mouth, the fire on his chest and the lightning at his hands, exactly as in the second image."),

    ("08_idle_fase2", REF_FASE2, 2,
     "He floats in place in his phase 2 form and breathes: the fire pouring out of his chest flickers and licks upward, the violet lightning jumps around his hands, the gold engraving in his armour pulses, his tendrils stretch out and curl back, and the golden ribbons and gears keep turning around him. Small movement, heavy and unhurried.",
     "loop", ""),

    ("09_fase2_ataque_grieta", REF_FASE2, 2,
     "He drives both arms downward and a jagged crack of blinding orange and white light tears through the air in front of him from top to bottom, splitting the frame. The crack holds open for a beat with shards of frozen time spilling out of it, then it seals shut and he settles back into his floating pose.",
     "oneshot", ""),

    ("10_fase2_ataque_reloj", REF_FASE2, 2,
     "A vast clock face of orange light forms in the air in front of him, its hands spinning backwards faster and faster. He drives one fist through the centre of it and the whole clock shatters outward into a storm of gold gear teeth and burning shards, then he settles back into his floating pose.",
     "oneshot", ""),

    ("11_fase2_ataque_enjambre", REF_FASE2, 2,
     "Both of his arms blur into a rapid sequence of strikes in place, and his dark tendrils lash forward with them, each strike leaving a hard orange afterimage of itself hanging in the air, until a dense fan of overlapping afterimages fills the space in front of him. The afterimages then collapse inward into him all at once and he settles back into his floating pose.",
     "oneshot", ""),

    # El definitivo. Se guarda para Veo Quality, asi que va escrito para que
    # pida pocas cosas y grandes: cuantos mas elementos nuevos se le piden a un
    # modelo de video, mas se le deshace el personaje.
    ("13_ataque_final", REF_FASE2, 2,
     "THE LAST SECOND, his finisher, in four clear beats and nothing else.\n\n1. He plants himself and throws both arms out wide, head back. Everything around him is pulled towards him: the golden ribbons whip inward and wind around his arms, and every floating gear swings in and locks into a single ring of gold turning slowly behind his shoulders.\n\n2. The band of fire on his chest is sucked inward and compresses into ONE small white point burning in the middle of his chest, so bright it is almost pure white. For a beat everything goes still and dark except that point and his orange eyes: the tendrils hang frozen, the ribbons stop, the ring stops.\n\n3. He brings both fists together in front of that point and drives them down. The white point detonates into a single expanding ring of white and gold light that sweeps outward across the whole frame and off the edges, and behind it a second slower ring of orange fire.\n\n4. He holds the follow-through: crouched forward over his fists, fire pouring back out of his chest, the gold ring behind him cracked and turning again, his tendrils drifting back down. Hold that pose completely still for the last frames.",
     "single",
     "This is the finisher, so it has to read big, but do NOT add anything that is not listed: no new tendrils, no new limbs, no summoned creatures, no clones of himself, no clock faces, no portals, no weapons, no lightning storm. The power comes from everything stopping in beat 2 and then one single ring in beat 3, not from filling the frame.\n\nEven at the brightest moment of the detonation, the background stays flat chroma green #00FF00 and the light never spills onto it or tints it. His body must stay readable the whole time: the fire and the light never cover his armour, his face or his silhouette."),

    ("12_muerte", REF_FASE2, 2,
     "The fire in his chest gutters and goes out, and the orange light drains from his eyes and his mouth. His tendrils go limp, unravel and fall away. The golden ribbons come apart and the gold gears drop out of their orbit one by one. His arms fall, and his armour crumbles from the hands and the feet inward into drifting gold dust, until there is nothing left but a slow fall of gold particles.",
     "oneshot",
     "IMPORTANT: the final frames must be completely empty flat green, with no character and no particles left."),
]

BOSS_README = """EL SENOR DEL TIEMPO SE HACE DISTINTO A LOS DEMAS
================================================

A los demas enemigos se les describe con palabras y Gemini los dibuja. Con el
jefe eso no funciona: cada clip salia con un diseno distinto porque el prompt
de texto le dejaba sitio para reinventarlo. Aqui ya tenemos su arte pintado, y
lo que se le pide al modelo es SOLO el movimiento.

LAS IMAGENES QUE SE ADJUNTAN (estan en esta misma carpeta, en videos/)
----------------------------------------------------------------------
  senor_del_tiempo_de_pie_verde_sin_portal.jpg      de pie, fase 1, en verde
  senor_del_tiempo_fase2_ASPECTO_fondo_verde_1.jpg  de pie, fase 2, en verde
  senor_del_tiempo_de_pie_verde_portal.jpg          la del portal: falta, ver
                                                    el PASO 0

La forma valida es la de PIE, entera, no la agachada. Las agachadas
(senor_del_tiempo_verde_fondo.png y senor_del_tiempo_fondo_verde_sin_portal.png)
se quedan solo como historial: no se usan, para que no cambie de postura de un
clip a otro.

De la fase 2 hay dos versiones. Se usa la _1, donde el fuego es una banda en el
pecho y se le ve bien el diseno. La _2 tiene mas resolucion pero el fuego le
tapa medio cuerpo; esta ahi por si hace falta.

PASO 0: FALTA LA DEL PORTAL, Y SOLO HACE FALTA PARA EL CLIP 00
--------------------------------------------------------------
No hay ninguna imagen de pie CON el portal detras, y el portal no se puede
pegar por programa sin que se note. Los clips 01 al 12 no la necesitan, asi que
esto se puede dejar para el final.

Cuando toque, se genera en la app de Gemini adjuntando
senor_del_tiempo_de_pie_verde_sin_portal.jpg y pidiendo esto:

    Keep this exact pixel art character exactly as he is: same pose, same
    armour, same helm, same face, same colours, same two arms and two legs,
    the same dark tendrils and the same golden ribbons and gears, same size
    and same position in the frame. Add one single thing: a huge swirling
    blue and cyan portal disc BEHIND him, filling the space behind his body
    like a full moon, its swirl turning around his chest. Everything outside
    the portal stays the same flat solid chroma green #00FF00. Do not redraw
    him, do not restyle him, do not move him, do not cover him with the
    portal, do not add or remove tendrils, do not add text or logos.

Se guarda en videos/ como senor_del_tiempo_de_pie_verde_portal.jpg.

EL ORDEN
--------
1. Del 01 al 06 se adjunta senor_del_tiempo_de_pie_verde_sin_portal.jpg.

2. 07_transformacion_fase2 lleva las DOS: la de pie como primer fotograma y la
   de la fase 2 como ultimo. Asi la fase 2 le sale igual que la imagen, que es
   la misma que se usa despues del 08 al 12.

3. Del 08 al 12 se adjunta senor_del_tiempo_fase2_ASPECTO_fondo_verde_1.jpg.

4. 00_portal_aparicion va el ultimo, cuando exista la imagen del PASO 0: se
   hace con las DOS, la del portal como primer fotograma y la de sin portal
   como ultimo.

En Flow, los clips de dos imagenes son "Frames to Video" (primer y ultimo
fotograma). En la app de Gemini se adjuntan las dos y el prompt ya explica
cual es cual.

LO QUE SALIO MAL LA PRIMERA VEZ Y YA ESTA CORREGIDO EN LOS PROMPTS
------------------------------------------------------------------
  - Se inventaba decenas de tentaculos nuevos hasta llenar el encuadre. Ahora
    cada prompt dice que los de la imagen son todos los que hay, que solo se
    mueven, y que tiene que haber el mismo numero en cada fotograma.
  - Intentaba sacarlo del portal y le deformaba las piernas. Ahora el prompt
    dice que ya esta fuera, que no sale de ningun sitio, y que lo unico que
    ocurre es que el portal se cierra solo detras de el.
  - El portal reventaba en pedazos. Ahora se prohibe: se encoge y se apaga.

SI AUN ASI SE LE DESHACE EL DISENO
----------------------------------
  - Que el clip sea corto. Cuanto mas largo, mas se aleja del original.
  - Pedir menos cosas a la vez: es mejor un clip por movimiento.
  - Si un clip sale bien hasta la mitad, vale igual: el extractor coge el
    tramo bueno con --desde y --hasta.
  - Y si un movimiento no hay manera, se hace en Unity con el sprite quieto
    (los lazos dorados, los engranajes y el fuego se pueden animar por codigo,
    que es como estan hechas las particulas de los niveles).
"""

README = """COMO USAR ESTOS PROMPTS
=======================

Son prompts de VIDEO, pensados para Gemini Video / Flow. Cada uno da un clip
del que luego se sacan los fotogramas para montar el sprite sheet.

1. Genera el clip con el prompt tal cual. Adjunta SIEMPRE la imagen de
   referencia que toque:
      - enemigos y mini-jefes  ->  Tools/refs/ekkar.png
      - Senor del Tiempo       ->  Tools/refs/senor_del_tiempo.png

2. Guarda el video en la subcarpeta 'videos' de ese enemigo, con el mismo
   nombre que el prompt:
      medieval/enemigos/soldado_hueso/videos/03_ataque.mp4

3. Monta las hojas:
      python Tools/build_enemy_sheets.py --enemigo soldado_hueso

   Saca los fotogramas, recorta el verde, se salta la esquina de la marca,
   elige el trozo util del clip (un ciclo si es bucle, una repeticion si es un
   golpe), reancla por los pies y arma la hoja + el manifiesto, igual que con
   las animaciones de Ekkar.

   Antes de nada, 'python Tools/build_enemy_sheets.py --listar' dice que clips
   ve. Para revisar como quedo, mira Tools/out/preview_enemigos/<enemigo>/:
   la "cebolla" ensena si el anclaje baila y la "tira" ensena los fotogramas
   elegidos sobre un cuadriculado.

   Si un clip sale mal cortado, se le dice a mano el trozo bueno:
      python Tools/build_enemy_sheets.py --enemigo soldado_hueso --anim 03_ataque --desde 3.5 --hasta 4.6 --fotogramas 12

POR QUE LOS PROMPTS SON TAN ESTRICTOS
-------------------------------------
Para poder cortar fotogramas utiles hacen falta cuatro cosas, y todas van
escritas en cada prompt:
  - camara completamente fija (si la camara se mueve, el sprite se desplaza)
  - nada de desenfoque de movimiento (arruina el pixel art)
  - el personaje animando en el sitio, sin cruzar el encuadre
  - fondo verde plano identico en todos los fotogramas

LA MARCA DE AGUA
----------------
Gemini estampa su marca en la esquina inferior derecha y no se puede quitar
desde el prompt. Por eso todos los prompts obligan a dejar esa esquina vacia
en verde: asi la marca nunca cae encima del personaje y no estorba al recortar
el encuadre. Ten en cuenta que las condiciones de uso de Google piden no
retirar esa marca, asi que decide tu que haces con el clip final.
"""


def main():
    total = 0
    for era, enemies in ENEMIES.items():
        base = os.path.join(PROMPTS, era, "enemigos")

        # los prompts viejos eran de imagen fija: se retiran
        old = os.path.join(base, "animaciones")
        if os.path.isdir(old):
            shutil.rmtree(old)
        if os.path.isdir(base):
            for f in os.listdir(base):
                if f.endswith(".txt") and os.path.isfile(os.path.join(base, f)):
                    os.remove(os.path.join(base, f))
        os.makedirs(base, exist_ok=True)

        with open(os.path.join(base, "_COMO_USAR.txt"), "w", encoding="utf-8") as f:
            f.write(README)

        roster = list(enemies)
        if era in MIDBOSS:
            roster.append(MIDBOSS[era])

        for name, desc, actions in roster:
            d = os.path.join(base, name)
            os.makedirs(d, exist_ok=True)

            anims = list(ANIMS)
            if "especial" in actions:
                anims.append(("06_ataque_especial", "especial", False))

            for filename, key, is_loop in anims:
                if key not in actions:
                    continue
                timing = loop_block() if is_loop else oneshot_block()
                txt = build(desc, actions[key], timing)
                with open(os.path.join(d, filename + ".txt"), "w", encoding="utf-8") as f:
                    f.write(txt)
                total += 1

        print(f"  {era:11s} {len(roster)} personajes")

    # ---- jefe final: image-to-video sobre su propio arte, ver BOSS_README
    boss_dir = os.path.join(PROMPTS, "hora_cero", "enemigos", "senor_del_tiempo")
    os.makedirs(boss_dir, exist_ok=True)
    with open(os.path.join(boss_dir, "_COMO_USAR.txt"), "w", encoding="utf-8") as f:
        f.write(BOSS_README)

    for filename, ref, fase, action, kind, extra in BOSS:
        timing = {"loop": loop_block(), "oneshot": oneshot_block()}.get(kind, single_block())
        txt = build_boss(ref, action, timing, fase, extra)
        with open(os.path.join(boss_dir, filename + ".txt"), "w", encoding="utf-8") as f:
            f.write(txt)
        total += 1
    print(f"  hora_cero   + Senor del Tiempo ({len(BOSS)} clips, image-to-video)")

    print(f"\n{total} prompts de video escritos en Tools/prompts/<era>/enemigos/")


if __name__ == "__main__":
    main()
