# Pixel art

One level per stanza of "Hope" is the thing with feathers, in the poem's order:

| Level | Stanza | Look |
|---|---|---|
| 1. Hope | "Hope" is the thing with feathers | Dawn: birch trunks and wisteria vines |
| 2. The Gale | And sweetest - in the Gale - is heard | Storm: cloud pillars and foam-capped rocks |
| 3. Extremity | I've heard it in the chillest land | Frozen night sea: icicles and ice pillars |

- `sprites/` holds the full set: bird frames, and per level the background, ground, whole pipes, a pickup, the title and the stanza panel.
- `previews/` holds title, gameplay and game-over mockups plus a gameplay GIF for each level.
- The game loads its pieces from `Assets/Resources/DickinsonSprites/` through `PixelArt.cs`. Pipes there are cut for tiling: the cap end stays fixed and the 18 body rows repeat.

## Regenerate

Needs Python 3 with Pillow (`pip install pillow`). From the repo root:

```bash
python Art/make_sprites.py
```

This rewrites `Art/sprites/`, `Art/previews/` and the PNGs in `Assets/Resources/DickinsonSprites/`. Keep the existing `.meta` files: they make Unity import the PNGs as crisp, uncompressed pixel art.
