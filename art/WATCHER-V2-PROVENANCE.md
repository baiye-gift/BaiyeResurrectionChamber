# Watcher production art provenance — 0.2.1

The v2 production sprites were generated with the built-in image_gen/imagegen tool on 2026-10-31. The transparent eight-part source atlas is preserved at `art/source/watcher_v2/production-parts.png`, replacing the temporary code-drawn graphics from 0.2.0. The original output remains in the user's Codex generated_images directory; the project consumes its tracked copy.

Prompt specification: `art/source/watcher_v2/production-prompt.md`. Requested original orthographic 2D cartoon medical machinery: ivory enamel, muted teal steel, copper hinges, cel shading, transparent background, no humans/game textures/environment/labels or placeholder diagrams. Parts are station chassis, sliding door, cot, docking clamp, drone chassis, canopy, gripper and scanner.

`tools/watcher_animation_assets.py` imports part regions by cropping/trimming/size normalization, authors pivots and animated translations/rotations/mirrors, and compiles through Kanimal. Light/beam/thrust effects are authored programmatically. No game textures or patient silhouettes are shipped. Editable sprites/SCML live under `art/source/watcher_v2/baiye_watcher_*`; consumed native triplets retain their IDs under `anim/assets/baiye_watcher_*`.

Each asset has 19 native clips including aliases. Doors, cot, clamp, scanner, canopy and grippers have verified changing transforms. Preview PNG/GIFs come from decoded native files. The full cycle composes native clips with a display transit path; standalone GIFs contain individual native clips only. None is a game screenshot, gameplay recording or proof of game runtime integration.
