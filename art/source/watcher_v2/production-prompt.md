# Production image generation specification

Tool: built-in `image_gen.imagegen`, `transparent_background: true`, 2026-10-31. No reference image or external API/model used.

Prompt: Production transparent 2D game sprite parts atlas for a side-view sci-fi colony simulation medical rescue station. Eight isolated parts, intended 2 columns and 4 rows, no borders/labels, generous transparent gutters. Consistent hand-inked cartoon game art: navy outlines, warm ivory enamel, muted sage/teal steel, copper hinges, restrained chunky cel shading and readable bolts/rubber/hoses. Strict orthographic elevation. No photorealism, 3D perspective, text, numbers, logos, humans, corpses, scenery, schematic or flat UI diagram.

Requested parts, in reading order:

1. A 4×3 station's static chassis with metal base/feet, empty left medical bay/backwall, upper-right empty hangar/docking arch, lower-right ECG terminal and oxygen bottle. No bed, doors, scanner or parked drone baked in.
2. Tall sliding aqua glass medical door in ivory frame with handle, seals and guide rollers.
3. Horizontal cot with cream mattress, sage pillow, rail, sliding metal tray and lower bracket; no patient or tall legs.
4. Inverted-U docking clamp with copper tips, actuator and ivory pistons; no drone.
5. Flying rescue drone chassis with two hover pods, medical cross and empty central cradle/rails; no glass, person, flame or long arms baked in.
6. Rounded aqua drone canopy with ivory rim/top hinge, matching the cradle.
7. Two-segment articulated ivory gripper with copper hinges and rubber claw, top shoulder pivot.
8. Medical scanning head with teal lens and L-shaped rail bracket; no beam/backdrop.

The returned eight-part layout has unequal columns. Import uses observed part boundaries rather than assuming an exact grid. The original 1086×1448 RGBA atlas remains unchanged; explicit regions and target dimensions are in the importer. Cropping is sprite ingestion, not substitute drawing.
