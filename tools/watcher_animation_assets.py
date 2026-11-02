"""Import original transparent sprite atlas and author real layered KAnim.

Cropping/resampling is asset ingestion; artwork is preserved from the imagegen
production atlas. Preview frames can be rendered from Kanimal-decoded binaries.
"""
from pathlib import Path
import argparse
import math
import os
import subprocess
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageChops

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT/'art/source/watcher_v3'
ATLAS = SOURCE/'production-parts.png'
REGIONS = {
    'chassis': (15, 10, 555, 380), 'cot': (555, 175, 950, 382),
    'lamp_arm': (955, 25, 1245, 365), 'lamp': (48, 405, 460, 710),
    'iv': (565, 400, 738, 728), 'drone': (744, 465, 1246, 688),
    'basket': (10, 790, 644, 983), 'strut': (680, 727, 797, 986),
    'shield': (895, 770, 1242, 972), 'rail': (38, 1070, 483, 1227),
    'rotor': (520, 1080, 902, 1220), 'case': (935, 1015, 1205, 1228),
}
SIZES = {'chassis': (400, 265), 'cot': (225, 112), 'lamp_arm': (110, 124),
         'lamp': (112, 82), 'iv': (40, 110), 'drone': (180, 78),
         'basket': (140, 43), 'strut': (14, 42), 'shield': (72, 32),
         'rail': (172, 43), 'rotor': (40, 15), 'case': (38, 31)}


def imported_parts():
    atlas = Image.open(ATLAS).convert('RGBA')
    result = {}
    for name, rect in REGIONS.items():
        crop = atlas.crop(rect)
        box = crop.getchannel('A').getbbox()
        assert box, 'empty production part: '+name
        result[name] = crop.crop(box).resize(SIZES[name], Image.Resampling.LANCZOS)
    return result


def ease(t):
    return .5-.5*math.cos(math.pi*max(0, min(1, t)))


CLIPS = ('idle', 'off', 'on', 'place', 'working_pre', 'working_loop',
         'working_pst', 'ui', 'loaded', 'treating', 'alarm', 'launch',
         'docking', 'transfer_in', 'release', 'away_idle', 'pickup',
         'unload', 'landing')
ONCE = {'launch', 'docking', 'transfer_in', 'release', 'pickup', 'unload', 'landing', 'working_pre', 'working_pst'}


def layers(kind, clip, t):
    foreground = clip.startswith('fg_')
    clip = clip.removeprefix('fg_')
    if kind == 'station':
        mode = {'working_pre': 'launch', 'working_loop': 'treating',
                'working_pst': 'release', 'loaded': 'treating'}.get(clip, clip)
        opening = math.sin(math.pi*t)**2 if mode in ('transfer_in', 'release') else 0
        scan = math.sin(2*math.pi*t) if mode == 'treating' else 0
        arm_angle = -5*ease(t) if mode == 'launch' else 4*scan
        if foreground:
            return [('body_hatch', 'body_hatch', dict(x=20, y=48)),
                    ('bed_rail', 'rail', dict(x=-40, y=93-34*opening, scale_y=1-.5*opening))]
        transfer = ease(t) if mode in ('transfer_in', 'unload') else 1
        out = [('chassis', 'chassis', dict(x=0, y=0)),
               ('cot', 'cot', dict(x=-40, y=30)),
               ('lamp_arm', 'lamp_arm', dict(x=-170, y=174, angle=arm_angle)),
               ('lamp', 'lamp', dict(x=-53+8*scan, y=246, angle=-5*scan)),
               ('medical_case', 'case', dict(x=86, y=42)),
               ('patient_anchor', 'patient_anchor', dict(x=-40, y=137)),
               ('drone_dock_anchor', 'drone_dock_anchor', dict(x=133, y=125)),
               ('transfer_anchor', 'transfer_anchor', dict(x=133-173*transfer, y=86.5+50.5*transfer+35*math.sin(math.pi*transfer))),
               ('body_transfer_anchor', 'body_transfer_anchor', dict(x=133-113*transfer, y=86.5-38.5*transfer+25*math.sin(math.pi*transfer)))]
        return out
    mode = {'working_pre': 'launch', 'working_pst': 'landing', 'docking': 'landing',
            'transfer_in': 'unload', 'treating': 'loaded', 'away_idle': 'working_loop'}.get(clip, clip)
    opening = math.sin(math.pi*t)**2 if mode in ('pickup', 'unload') else 0
    lower = 26*opening
    if foreground:
        return [('shield_left', 'shield', dict(x=-34-28*opening, y=-84-lower, angle=-12*opening)),
                ('shield_right', 'shield', dict(x=34+28*opening, y=-84-lower, angle=12*opening, scale_x=-1))]
    out = [('strut_left', 'strut', dict(x=-49, y=-15, scale_y=(62+lower)/42)),
           ('strut_right', 'strut', dict(x=49, y=-15, scale_y=(62+lower)/42, scale_x=-1)),
           ('basket', 'basket', dict(x=0, y=-75-lower)),
           ('drone_body', 'drone', dict(x=0, y=0)),
           ('patient_anchor', 'patient_anchor', dict(x=0, y=-70-lower)),
           ('snapTo_pivot', 'snapTo_pivot', dict(x=0, y=-70-lower))]
    flying = mode not in ('idle', 'off', 'place', 'ui')
    for i, (x, y) in enumerate(((-42, 26), (62, 24), (-63, 7), (64, -5))):
        out.append(('rotor'+str(i), 'rotor', dict(x=x, y=y, scale_y=(.35+.65*abs(math.cos(8*math.pi*t+i))) if flying else 1)))
    return out


def sprite_set(kind, parts):
    anchor = Image.new('RGBA', (1, 1))
    result = {}
    if kind == 'station':
        result.update({n: (parts[n], (.5, 0)) for n in ('chassis', 'cot', 'case')})
        result.update({n: (parts[n], (.5, .5)) for n in ('lamp', 'rail')})
        result['lamp_arm'] = (parts['lamp_arm'], (.15, .05))
        result['body_hatch'] = (parts['shield'].resize((72, 26), Image.Resampling.LANCZOS), (.5, .5))
        result['drone_dock_anchor'] = (anchor, (.5, .5))
        for n in ('transfer_anchor', 'body_transfer_anchor'): result[n] = (anchor, (.5, .5))
    else:
        result.update({n: (parts[n], (.5, 1 if n == 'strut' else .5)) for n in ('drone', 'basket', 'strut', 'shield', 'rotor')})
        result['snapTo_pivot'] = (anchor, (.5, .5))
    result['patient_anchor'] = (anchor, (.5, .5))
    # The icon is assembled from the exact same imported production parts.
    icon = Image.new('RGBA', (128, 128)); body = parts['chassis' if kind == 'station' else 'drone'].copy()
    body.thumbnail((120, 120), Image.Resampling.LANCZOS)
    icon.alpha_composite(body, ((128-body.width)//2, (128-body.height)//2))
    result['ui'] = (icon, (.5, .5))
    return result


def compile_asset(kind, parts, kanimal):
    name = 'baiye_watcher_'+kind
    folder = SOURCE/name; folder.mkdir(parents=True, exist_ok=True)
    dest = ROOT/'anim/assets'/name; dest.mkdir(parents=True, exist_ok=True)
    sprites = sprite_set(kind, parts)
    root = ET.Element('spriter_data', scml_version='1.0', generator='BaiyeWatcher', generator_version='0.2.2')
    files = ET.SubElement(root, 'folder', id='0'); ids = {}
    for i, (symbol, (im, pivot)) in enumerate(sprites.items()):
        ids[symbol] = i; filename = symbol+'_0.png'; im.save(folder/filename)
        ET.SubElement(files, 'file', id=str(i), name=filename, width=str(im.width), height=str(im.height), pivot_x=str(pivot[0]), pivot_y=str(pivot[1]))
    entity = ET.SubElement(root, 'entity', id='0', name=name)
    all_clips = CLIPS + tuple('fg_'+c for c in CLIPS)
    for idx, clip in enumerate(all_clips):
        base = clip.removeprefix('fg_')
        count = 1 if base in ('off', 'place', 'ui') else 16 if base in ('idle', 'away_idle', 'treating') else 12
        anim = ET.SubElement(entity, 'animation', id=str(idx), name=clip, length=str(count*100), interval='100', looping='false' if base in ONCE else 'true')
        main = ET.SubElement(anim, 'mainline'); timelines = {}
        for frame in range(count):
            t = frame/(count-1) if base in ONCE else frame/count
            parts_frame = [('ui', 'ui', dict(x=0, y=0))] if clip == 'ui' else layers(kind, clip, t)
            key = ET.SubElement(main, 'key', id=str(frame), time=str(frame*100))
            for z, (label, sprite, attrs) in enumerate(parts_frame):
                if label not in timelines: timelines[label] = ET.SubElement(anim, 'timeline', id=str(len(timelines)), name=label)
                tl = timelines[label]; key_id = str(len(tl.findall('key')))
                tk = ET.SubElement(tl, 'key', id=key_id, time=str(frame*100))
                ET.SubElement(tk, 'object', folder='0', file=str(ids[sprite]), **{k: str(round(v, 5)) for k, v in attrs.items()})
                ET.SubElement(key, 'object_ref', id=str(z), timeline=tl.attrib['id'], key=key_id, z_index=str(z))
    ET.indent(root); scml = folder/(name+'.scml')
    ET.ElementTree(root).write(scml, encoding='utf-8', xml_declaration=True)
    # Building-menu icon uses the complete cot/lamp/platform assembly.
    icon = Image.new('RGBA', (450, 330))
    render(icon, SOURCE, kind, 'idle', 0, (220, 305) if kind == 'station' else (220, 155))
    icon = icon.crop(icon.getchannel('A').getbbox()); icon.thumbnail((120, 120), Image.Resampling.LANCZOS)
    square = Image.new('RGBA', (128, 128)); square.alpha_composite(icon, ((128-icon.width)//2, (128-icon.height)//2)); square.save(folder/'ui_0.png')
    env = os.environ.copy(); env['DOTNET_ROLL_FORWARD'] = 'Major'
    subprocess.run([kanimal, 'kanim', str(scml), '-o', str(dest)], env=env, check=True)


def objects(tree, clip_name, frame):
    files = {f.attrib['id']: f.attrib for f in tree.findall('folder/file')}
    clip = next(a for a in tree.findall('entity/animation') if a.attrib['name'] == clip_name)
    timelines = {t.attrib['id']: t for t in clip.findall('timeline')}
    keys = clip.find('mainline').findall('key'); key = keys[min(frame, len(keys)-1)]
    result = []
    for ref in sorted(key.findall('object_ref'), key=lambda r: int(r.attrib['z_index'])):
        obj = next(k for k in timelines[ref.attrib['timeline']] if k.attrib['id'] == ref.attrib['key']).find('object').attrib
        result.append((files[obj['file']], obj))
    return result


def verify(kind, decoded=None):
    name = 'baiye_watcher_'+kind; folder = SOURCE/name
    tree = ET.parse(folder/(name+'.scml')).getroot()
    clips = {a.attrib['name']: a for a in tree.findall('entity/animation')}
    native = ROOT/'anim/assets'/name
    assert (native/(name+'_anim.bytes')).read_bytes()[:4] == b'ANIM'
    build = (native/(name+'_build.bytes')).read_bytes()
    assert build[:4] == b'BILD' and b'patient_anchor' in build
    assert ('drone_dock_anchor' if kind == 'station' else 'snapTo_pivot').encode() in build
    all_clips = CLIPS + tuple('fg_'+c for c in CLIPS)
    assert set(clips) == set(all_clips)
    # Prove real component motion, not merely multiple identical frame names.
    checks = (('launch', 'lamp_arm'), ('transfer_in', 'transfer_anchor'), ('treating', 'lamp'), ('fg_release', 'bed_rail')) if kind == 'station' else (('pickup', 'basket'), ('pickup', 'strut_left'), ('fg_unload', 'shield_right'))
    for clip, label in checks:
        tl = next(t for t in clips[clip].findall('timeline') if t.attrib['name'] == label)
        values = {tuple(sorted(k.find('object').attrib.items())) for k in tl}
        assert len(values) >= 6, 'static mechanical clip: '+clip+'/'+label
    if decoded:
        restored = Path(decoded)/name; native_tree = ET.parse(restored/(name+'.scml')).getroot()
        for clip in all_clips:
            native_clip = next(a for a in native_tree.findall('entity/animation') if a.attrib['name'] == clip)
            assert len(clips[clip].find('mainline')) == len(native_clip.find('mainline'))
            for i in range(len(clips[clip].find('mainline'))):
                def canonical(data):
                    result = []
                    for f, o in data:
                        theta = math.radians(float(o.get('angle', 0)))
                        sx, sy = float(o.get('scale_x', 1)), float(o.get('scale_y', 1))
                        # Native SCML can express an X mirror as rotation180
                        # plus a Y mirror. Compare the resulting affine basis.
                        values = (float(o.get('x', 0)), float(o.get('y', 0)),
                                  math.cos(theta)*sx, math.sin(theta)*sx,
                                  -math.sin(theta)*sy, math.cos(theta)*sy)
                        result.append((f['name'],)+values)
                    return sorted(result)
                before, after = canonical(objects(tree, clip, i)), canonical(objects(native_tree, clip, i))
                assert [f['name'] for f,o in objects(tree,clip,i)] == [f['name'] for f,o in objects(native_tree,clip,i)], (kind,clip,i,'native layer order changed')
                # KAnim stores single precision. Rounding at a decimal halfway
                # point creates false mismatches (0.6205 vs 0.6205000289).
                assert len(before) == len(after) and all(a[0] == b[0] and all(abs(x-y) < .0001 for x,y in zip(a[1:], b[1:])) for a,b in zip(before,after)), (kind, clip, i, 'native transform changed')
        for f in tree.findall('folder/file'):
            before = Image.open(folder/f.attrib['name']).convert('RGBA'); after = Image.open(restored/f.attrib['name']).convert('RGBA')
            assert before.size == after.size and before.getchannel('A').tobytes() == after.getchannel('A').tobytes()
            black = Image.new('RGBA', before.size, (0, 0, 0, 255))
            diff = ImageChops.difference(Image.alpha_composite(black, before).convert('RGB'), Image.alpha_composite(black, after).convert('RGB'))
            assert max(high for low, high in diff.getextrema()) <= 1
        def point(clip, frame, name):
            return next((float(o.get('x',0)),float(o.get('y',0))) for f,o in objects(native_tree,clip,frame) if f['name']==name+'_0.png')
        if kind == 'station':
            assert point('idle',0,'patient_anchor') == (-40,137)
            assert point('idle',0,'drone_dock_anchor') == (133,125)
            assert point('transfer_in',0,'transfer_anchor') == (133,86.5)
            assert point('transfer_in',11,'transfer_anchor') == (-40,137)
            assert point('transfer_in',11,'body_transfer_anchor') == (20,48)
        else:
            assert point('loaded',0,'patient_anchor') == (0,-70)
            assert point('loaded',0,'snapTo_pivot') == (0,-70)
            # Same worst-case size limit as ChamberPatientVisual, 100px/m.
            assert -70 + (.85*1.05/1.5*100)/2 < -SIZES['drone'][1]/2
            assert 1.05*100 < SIZES['basket'][0]
    print('PASS', name, '38 native background/foreground clips; component motion verified; decoded match' if decoded else '38 clips; component motion verified')


def render(canvas, directory, kind, clip, frame, origin, scale=1, foreground=True):
    name = 'baiye_watcher_'+kind; folder = Path(directory)/name
    tree = ET.parse(folder/(name+'.scml')).getroot()
    for file, obj in objects(tree, clip, frame):
        sprite = Image.open(folder/file['name']).convert('RGBA')
        sx, sy = float(obj.get('scale_x', 1)), float(obj.get('scale_y', 1))
        pivot = (float(file.get('pivot_x', .5)), float(file.get('pivot_y', .5)))
        sprite = sprite.resize((max(1, round(sprite.width*abs(sx)*scale)), max(1, round(sprite.height*abs(sy)*scale))), Image.Resampling.LANCZOS)
        if sx < 0: sprite = sprite.transpose(Image.Transpose.FLIP_LEFT_RIGHT); pivot = (1-pivot[0], pivot[1])
        if sy < 0: sprite = sprite.transpose(Image.Transpose.FLIP_TOP_BOTTOM); pivot = (pivot[0], 1-pivot[1])
        # Rotate on a padded pivot canvas, matching SCML's y-up transforms.
        pad = max(sprite.size)*3+4; layer = Image.new('RGBA', (pad, pad)); center = pad//2
        layer.alpha_composite(sprite, (round(center-pivot[0]*sprite.width), round(center-(1-pivot[1])*sprite.height)))
        layer = layer.rotate(float(obj.get('angle', 0)), Image.Resampling.BICUBIC, expand=False)
        x = origin[0]+float(obj.get('x', 0))*scale-center
        y = origin[1]-float(obj.get('y', 0))*scale-center
        canvas.alpha_composite(layer, (round(x), round(y)))
    if foreground and not clip.startswith('fg_'):
        render(canvas, directory, kind, 'fg_'+clip, frame, origin, scale, False)


def export_previews(directory):
    out = ROOT/'art/preview'; out.mkdir(parents=True, exist_ok=True)
    static = Image.new('RGBA', (720, 540))
    render(static, directory, 'station', 'idle', 0, (350, 470), 1.6)
    render(static, directory, 'drone', 'idle', 0, (350+133*1.6, 470-125*1.6), .55*1.6)
    static.save(out/'watcher-station-final.png')
    frames = []
    # Only decoded, actually compiled KAnim frames are composited here.
    sequence = [('idle','idle',16),('launch','launch',12),('away_idle','working_loop',16),
                ('away_idle','pickup',12),('away_idle','loaded',16),('docking','landing',12),
                ('transfer_in','unload',12),('treating','idle',16),('release','idle',12)]
    for index, (station_clip, drone_clip, count) in enumerate(sequence):
        for frame in range(count):
            t = frame/max(1, count-1); canvas = Image.new('RGBA', (960, 540), '#1a292c')
            origin = (330, 470); s = 1.45
            render(canvas, directory, 'station', station_clip, frame, origin, s)
            dock = (origin[0]+133*s, origin[1]-125*s)
            away = (800, 270)
            if index == 1: pos = tuple(a+(b-a)*ease(t) for a,b in zip(dock,away)); size = (.55+.45*ease(t))*s
            elif index in (2,3): pos, size = away,s
            elif index == 4: pos = tuple(a+(b-a)*ease(t) for a,b in zip(away,dock)); size = (1-.45*ease(t))*s
            else: pos,size = dock,.55*s
            render(canvas, directory, 'drone', drone_clip, frame, pos, size)
            frames.append(canvas.convert('RGB'))
    save_gif(frames, out/'watcher-native-cycle.gif')
    # Also provide mechanical clips individually, with no invented flight path.
    for kind, clip in (('station','treating'),('station','transfer_in'),('drone','pickup')):
        count = 16 if clip == 'treating' else 12; images = []
        for i in range(count):
            canvas = Image.new('RGBA', (720, 500), '#1a292c')
            render(canvas,directory,kind,clip,i,(350,470) if kind=='station' else (350,170),1.5 if kind=='station' else 2.2)
            images.append(canvas.convert('RGB'))
        save_gif(images, out/(kind+'-'+clip+'.gif'))
    print('EXPORTED decoded native static PNG and animated GIFs to', out)


def save_gif(frames, path):
    # Shared palette and retained-frame disposal preserve static machinery
    # between frames, avoiding a large full-canvas image for every tick.
    palette = frames[0].quantize(colors=160, method=Image.Quantize.MEDIANCUT)
    indexed = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    indexed[0].save(path, save_all=True, append_images=indexed[1:], duration=100,
                    loop=0, disposal=1, optimize=True)


def main():
    p = argparse.ArgumentParser(); p.add_argument('--kanimal'); p.add_argument('--verify-only', action='store_true'); p.add_argument('--roundtrip'); p.add_argument('--render-native')
    args = p.parse_args()
    if not args.verify_only and not args.render_native:
        if not args.kanimal: p.error('--kanimal is required for compilation')
        parts = imported_parts()
        for kind in ('station','drone'): compile_asset(kind,parts,args.kanimal)
    for kind in ('station','drone'): verify(kind,args.roundtrip or args.render_native)
    if args.render_native: export_previews(args.render_native)


if __name__ == '__main__': main()
