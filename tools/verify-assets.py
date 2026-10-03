"""Validate actual native asset names and generate source-stage previews."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageChops
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1];NAME='baiye_reconstruction'
source=ROOT/'art/source'/NAME;dest=ROOT/'anim/assets'/NAME
tree=ET.parse(source/(NAME+'.scml')).getroot()
files={f.attrib['id']:f.attrib for f in tree.findall('folder/file')}
assert not any(f['name'].startswith('patient_') and not f['name'].startswith('patient_anchor_') for f in files.values()),'generic patient placeholder still shipped'
assert Image.open(source/'patient_anchor_0.png').getchannel('A').getextrema()==(0,0),'anchor is visible'
anims={a.attrib['name']:a for a in tree.findall('entity/animation')}
required=['idle','loaded','reconstruct','recover','paused','alarm','release']
assert all(x in anims for x in required)
for kind in ('pod_door','pod_fluid'):
    a=anims[kind];keys=a.find('mainline').findall('key');assert len(keys)==51
    tls={t.attrib['id']:t for t in a.findall('timeline')}
    assert all(tls[r.attrib['timeline']].attrib['name']=='patient_anchor' for r in keys[0].findall('object_ref')),'zero level still visible'
    assert len(keys[-1].findall('object_ref'))==2,'full overlay absent'
native=(dest/(NAME+'_anim.bytes')).read_bytes()
for kind in required:
    assert kind.encode() in native,kind
    assert ('fx_'+kind).encode() in native,kind+' missing foreground effects'
for f in files.values():
    im=Image.open(source/f['name']);assert im.mode=='RGBA' and im.size==(int(f['width']),int(f['height']))
assert Image.open(source/'ui_0.png').size==(128,128)
housing=Image.open(ROOT/'art/source/chamber-housing-chairless.png').convert('RGBA')
assert housing.getchannel('A').getextrema()==(0,255)
housing.thumbnail((800,800),Image.Resampling.LANCZOS)
assert Image.open(source/'body_0.png').tobytes()==housing.tobytes(),'compiled housing does not use approved chairless source'
assert max(Image.open(next(dest.glob('*.png'))).size)<=2048
out=ROOT/'art/preview';out.mkdir(parents=True,exist_ok=True)
def composite(canvas,kind,index):
    a=anims[kind];keys=a.find('mainline').findall('key');index%=len(keys)
    tls={t.attrib['id']:t for t in a.findall('timeline')}
    for ref in sorted(keys[index].findall('object_ref'),key=lambda r:int(r.attrib['z_index'])):
        o=next(k for k in tls[ref.attrib['timeline']].findall('key') if k.attrib['id']==ref.attrib['key']).find('object').attrib;f=files[o['file']];im=Image.open(source/f['name']).convert('RGBA')
        im=im.resize((max(1,round(im.width*float(o.get('scale_x',1)))),max(1,round(im.height*float(o.get('scale_y',1))))),Image.Resampling.LANCZOS)
        im.putalpha(im.getchannel('A').point(lambda v:round(v*float(o.get('a',1)))))
        x=240+float(o.get('x',0))-float(f['pivot_x'])*im.width
        y=446-float(o.get('y',0))-(1-float(f['pivot_y']))*im.height
        canvas.alpha_composite(im,(round(x),round(y)))
def frame(kind,index):
    canvas=Image.new('RGBA',(480,480),(33,45,49,255));ImageDraw.Draw(canvas).line((0,446,480,446),fill=(137,151,148,255),width=2)
    composite(canvas,kind,index);composite(canvas,'fx_'+kind,index)
    if kind not in ('idle','release'):
        composite(canvas,'pod_fluid',50);composite(canvas,'pod_door',50)
    ImageDraw.Draw(canvas).text((12,12),'SOURCE EFFECT PREVIEW: '+kind,fill=(230,237,227,255))
    ImageDraw.Draw(canvas).text((12,28),'Runtime patient is not included in this preview',fill=(193,210,210,255));return canvas
sheet=Image.new('RGBA',(480*4,480*2),(33,45,49,255))
for i,kind in enumerate(required):
    a=anims[kind];tls={t.attrib['name']:t for t in a.findall('timeline')}
    body=[tuple(sorted(k.find('object').attrib.items())) for k in tls['body'].findall('key')];assert len(set(body))==1,'housing moved'
    assert 'patient_anchor' in tls,'patient attachment missing'
    fx={t.attrib['name']:t for t in anims['fx_'+kind].findall('timeline')}
    if kind in ('reconstruct','recover'):
        positions={k.find('object').attrib['y'] for k in fx['scanner'].findall('key')};assert len(positions)>8,'scanner stationary'
        assert 'ring0' in fx,'energy ring missing'
    if kind=='reconstruct':assert all('particle'+str(j) in fx for j in range(10)),'converging particles missing'
    if kind in ('idle','paused','alarm'):
        assert not any(x.startswith(('scanner','ring','particle','supply','bubble')) for x in list(tls)+list(fx)),'stopped chamber has treatment motion'
    for a in (anims[kind],anims['fx_'+kind]):
        for timeline in a.findall('timeline'):
            assert all('a' not in k.find('object').attrib for k in timeline.findall('key')),'relying on SCML alpha'
    images=[frame(kind,j).convert('RGB') for j in range(len(a.find('mainline').findall('key')))];images[0].save(out/(kind+'.png'))
    if len(images)>1:images[0].save(out/(kind+'.gif'),save_all=True,append_images=images[1:],duration=100,loop=0)
    sheet.alpha_composite(frame(kind,0),((i%4)*480,(i//4)*480))
    print('PASS native/source stage',kind)
sheet.convert('RGB').save(out/'stages.png')
print('PASS no placeholder; independent 51-level door/fluid; invisible anchor; baked opacity; static stopped stages; energy rings and converging particles')
print('PASS transparent housing, 128px icon, atlas bounds, fixed housing and mechanical motion')
roundtrip=ROOT/'dist/native-animation-roundtrip'
native_scml=roundtrip/(NAME+'.scml')
if native_scml.exists() and native_scml.stat().st_mtime>=(dest/(NAME+'_anim.bytes')).stat().st_mtime:
    decoded=ET.parse(native_scml).getroot()
    decoded_files={f.attrib['id']:f.attrib for f in decoded.findall('folder/file')}
    assert {f['name'] for f in decoded_files.values()}=={f['name'] for f in files.values()},'native sprite set changed'
    for f in decoded_files.values():
        original=Image.open(source/f['name']).convert('RGBA');restored=Image.open(roundtrip/f['name']).convert('RGBA')
        assert original.size==restored.size and original.getchannel('A').tobytes()==restored.getchannel('A').tobytes(),'native alpha changed: '+f['name']
        # Kanimal's PNG library rounds some RGB channels down by one during
        # atlas extraction. Check visible color at 8-bit rounding tolerance;
        # RGB hidden behind zero alpha is immaterial to rendering.
        black=Image.new('RGBA',original.size,(0,0,0,255))
        delta=ImageChops.difference(Image.alpha_composite(black,original).convert('RGB'),Image.alpha_composite(black,restored).convert('RGB'))
        assert max(high for low,high in delta.getextrema())<=1,'native visible color changed: '+f['name']
    decoded_anims={a.attrib['name']:a for a in decoded.findall('entity/animation')}
    assert set(decoded_anims)==set(anims),'native animation set changed'
    for kind in anims:
        assert len(decoded_anims[kind].find('mainline').findall('key'))==len(anims[kind].find('mainline').findall('key')),'native frame count changed: '+kind
    for kind in ('idle','fx_idle','paused','fx_paused','alarm','fx_alarm'):
        a=decoded_anims[kind];tls={t.attrib['id']:t for t in a.findall('timeline')}
        visible=[]
        for ref in a.find('mainline/key').findall('object_ref'):
            obj=next(k for k in tls[ref.attrib['timeline']].findall('key') if k.attrib['id']==ref.attrib['key']).find('object')
            visible.append(decoded_files[obj.attrib['file']]['name'])
        assert not any(x.startswith(('scanner','ring','particle','flow','bubble')) for x in visible),'native stopped stage includes treatment effects'
    print('PASS native roundtrip: '+str(len(anims))+' animations, exact alpha, RGB rounding <=1/255, frame counts, stopped motion layers absent')
else:
    print('Native roundtrip not checked (export native assets with Kanimal scml first)')
