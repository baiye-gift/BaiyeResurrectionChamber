# Watcher medical production art v3

Generated 2026-11-01 with the built-in image_gen tool, transparent_background=true.
Reference: art/designs/watcher-open-medical/watcher-open-medical-proposal.png,
the user-approved open medical station. It is a structural reference, not shipped art.
Source: art/source/watcher_v3/production-parts.png, 1254 x 1254 RGBA, alpha 0..255.
SHA256: 59f90f1434465f4607a10c7c7bdd57184b513936ef2269df65b9af7d8a524397.
Original generated filename: exec-c1d0624d-afb9-4e5d-8e2d-6491070bd4b6.png.

The full generation prompt is recorded beside the source. The generated layout
does not exactly follow the requested uniform grid; crop rectangles were chosen
from the actual output, not inferred from that request. All 12 pieces are retained
as crops; the extra IV part is not used because an IV is already in the stand.
The small opaque storage hatch reuses the generated ceramic shield art.

Production processing: alpha bounding-box crop, Lanczos size normalization,
independent sprites and authored SCML affine animation, native Kanimal compilation.
No generated humanoid is included. Runtime patient appearance comes from the game.
Both native groups contain 19 background/anchor clips and 19 matching foreground
clips. Exact crops, dimensions, pivots, paths and timings are in
tools/watcher_animation_assets.py; native-decoded frame and pixel comparisons
are the preview source. The composite flight path is an asset preview, not a
runtime recording. No game or test save was launched.
