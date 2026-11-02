# Watcher 0.2.0 temporary mechanical art provenance

Historical record only. Superseded by the production bitmap parts and layered animations documented in `WATCHER-V2-PROVENANCE.md`.

The Watcher station and drone are original programmatically authored mechanical sprites, created for this mod on 2026-10-31 with `tools/build-watcher-assets.py` using Pillow drawing primitives. No game textures, third-party illustrations, generated human silhouettes or external image assets are embedded.

- Station: 400×300 transparent source, emergency cot, drone dock, oxygen cylinder and ECG terminal.
- Drone: 180×90 transparent source, protected rescue capsule, propulsion pods and medical emblem.
- Each native asset has eleven clips and an invisible patient attachment. Patient appearance, if present in-game, must come from the actual patient's runtime rendering and is absent from source previews.
- Source SCML and PNG sprites are editable under `art/source/baiye_watcher_*`.
- Native KAnim and texture atlas outputs live under `anim/assets/baiye_watcher_*`.
- `art/preview/watcher-assets.png` is an authored asset preview, not an in-game screenshot or evidence of successful rescue.

Build with the locally installed Kanimal converter, then export the three output files of each asset through Kanimal `scml` to check roundtrip metadata/sprite consistency. `build-watcher-assets.py --verify-only --roundtrip <decoded-parent>` checks clip counts, attachment positions, exact sprite alpha and visible color within one channel level. These checks do not run Unity or establish in-game scale, layering or behavior.
