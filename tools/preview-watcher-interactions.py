"""Render actual native human quads with the production C# sampler/solver.

Requires a local extraction of the user's game. No game texture or body animation
is copied into this repository or distributed as a mod dependency.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import subprocess
from PIL import Image, ImageDraw, ImageFont
from native_kanim_evidence import NativeAsset

ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser()
parser.add_argument('--recon',required=True)
parser.add_argument('--decoded',required=True)
parser.add_argument('--dotnet',required=True)
parser.add_argument('--sampler',required=True)
args=parser.parse_args(); recon=Path(args.recon); decoded=Path(args.decoded)
samples_path=recon/'production-samples.json'
subprocess.run([args.dotnet,args.sampler,'--export-interactions',str(samples_path)],check=True)
samples=json.loads(samples_path.read_text()); by_stage={}
for row in samples:by_stage.setdefault(row['stage'],[]).append(row)
medical=NativeAsset(recon/'assets/body_comp_default_build.bytes',recon/'assets/anim_healing_bed_anim.bytes',recon/'healing-body')
incap=NativeAsset(recon/'assets/body_comp_default_build.bytes',recon/'assets/anim_incapacitated_anim.bytes',recon/'healing-body')
def mod_asset(kind):
    name='baiye_watcher_'+kind; folder=ROOT/'anim/assets'/name
    return NativeAsset(folder/(name+'_build.bytes'),folder/(name+'_anim.bytes'),decoded/name)
station,drone=mod_asset('station'),mod_asset('drone')
reference=medical.geometry('working_loop',0)
geometries=[medical.geometry('working_loop',i) for i in range(114)]
reference_width=max(g['Width'] for g in geometries);reference_height=max(g['Height'] for g in geometries)
requests=[];scenes=[]
for flipped in (False,True):
    phases=[('Pickup',range(0,121,3)),('Carry',range(0,121,3)),('Place',range(0,121,3)),('Settle',range(0,121,3)),('Bed',range(121)),('Exit',range(0,121,2))]
    if not flipped:phases.append(('EmergencyExit',range(0,121,3)))
    for stage,indices in phases:
        for index in indices:
            row=by_stage[stage][index]; p=row['progress'];ease=row['ease']; asset=medical if row['medical'] else incap
            assert len(asset.clips[row['clip']]['frames'])==row['count']
            clip,frame=row['clip'],row['frame']
            inp=dict(Registration=0,AnchorX=0,AnchorY=0,SourceX=3.8,SourceY=0,DestinationX=-2.5,DestinationY=0,
                     Width=1.05,Height=.5,Progress=p,SourceScale=.005,DestinationScale=.005,Facing=-1 if flipped else 1)
            if stage=='Pickup':
                drone_position=(3.8,1.5);ratio=1;mechanical='pickup';station_clip='away_idle'
                # Exact raw anchor from the selected compiled mechanical frame.
                raw=drone.clips['pickup']['frames'][min(11,int(p*12))]
                anchor=next(e for e in raw if drone.names.get(e['symbol'])=='patient_anchor')['matrix']
                inp.update(Registration=1,AnchorX=3.8+anchor[2]*.005,AnchorY=1.5-anchor[5]*.005)
            elif stage=='Carry':
                drone_position=(3.8+(1.33-3.8)*ease,1.5+(1.25-1.5)*ease);ratio=1+(.55-1)*ease;mechanical='loaded';station_clip='away_idle'
                inp.update(AnchorX=drone_position[0],AnchorY=drone_position[1]-.7*ratio,Width=1.05*ratio,Height=.5*ratio)
            elif stage=='Place':
                drone_position=(1.33,1.25);ratio=.55;mechanical='unload';station_clip='transfer_in'
                inp.update(AnchorX=row['transferX'],AnchorY=row['transferY'],Width=row['transferWidth'],Height=row['transferHeight'],Facing=row['facingX'] if flipped else 1)
            else:
                drone_position=(1.33,1.25);ratio=.55;mechanical='idle';station_clip='treating' if stage=='Bed' else 'transfer_in' if stage=='Settle' else 'release'
                inp.update(Registration=4 if stage=='EmergencyExit' else 5 if stage=='Settle' else 2 if stage=='Bed' else 3,AnchorX=-.4,AnchorY=1.37,Width=1.5,Height=.85,Facing=-1)
            ident=f'{int(flipped)}-{stage}-{index}'
            request=dict(Id=ident,Input=inp,Current=asset.geometry(clip,frame),Reference=reference,ReferenceWidth=reference_width,ReferenceHeight=reference_height,Previous=incap.geometry('place',33),Entry=medical.geometry('working_pre',24))
            if stage=='EmergencyExit':request.update(Previous=medical.geometry('working_loop',38),Entry=incap.geometry('place',17));request['Input']['Progress']=row['emergencyMotion']
            requests.append(request);scenes.append(dict(id=ident,stage=stage,index=index,flipped=flipped,row=row,drone_position=drone_position,ratio=ratio,mechanical=mechanical,station_clip=station_clip))
request_path=recon/'pose-requests.json';result_path=recon/'pose-solutions.json'
request_path.write_text(json.dumps(requests),encoding='utf-8')
subprocess.run([args.dotnet,args.sampler,'--solve-poses',str(request_path),str(result_path)],check=True)
solutions={r['Id']:r['Pose'] for r in json.loads(result_path.read_text())}
request_map={r['Id']:r for r in requests}
# These checks inspect the same solved quads rendered below. Matching only a
# character's center is insufficient: the native lying head has another view.
def head_quad(key):
    g=request_map[key]['Current'];pose=solutions[key]
    if pose['Affine']:a,b,c,d=pose['M00'],pose['M01'],pose['M10'],pose['M11']
    else:
        angle=math.radians(pose['Rotation']);a,b,c,d=math.cos(angle)*pose['Scale']*pose['Horizontal'],-math.sin(angle)*pose['Scale'],math.sin(angle)*pose['Scale']*pose['Horizontal'],math.cos(angle)*pose['Scale']
    return (pose['X']+a*g['HeadX']+b*g['HeadY'],pose['Y']+c*g['HeadX']+d*g['HeadY'],a*g['HeadDX']+b*g['HeadDY'],c*g['HeadDX']+d*g['HeadDY'],a*g['HeadEX']+b*g['HeadEY'],c*g['HeadEX']+d*g['HeadEY'])
seams={}
for first,last in (('0-Place-120','0-Settle-0'),('0-Settle-120','0-Bed-0'),('0-Bed-120','0-Exit-0')):
    difference=max(abs(a-b) for a,b in zip(head_quad(first),head_quad(last)));seams[first+' -> '+last]=difference
    assert difference<(.00001 if 'Place' in first else .005),seams
affine_determinants={}
for stage in ('Settle','EmergencyExit'):
    values=[p['M00']*p['M11']-p['M01']*p['M10'] for key,p in solutions.items() if '-'+stage+'-' in key]
    assert min(values)*max(values)>0 and min(abs(v) for v in values)>1e-7,(stage,values)
    affine_determinants[stage]=dict(minimum=min(values),maximum=max(values),no_singular_frame=True)
# Raw world geometry is checked independently of SCML and PNG sizes.
def native_width(asset,name):
    frames=next(v for k,v in asset.symbols.items() if asset.names[k]==name);box=frames[0]['box'];return box[2]-box[0]
assert native_width(station,'chassis')*.005==4
assert native_width(drone,'drone')*.005==1.8
out=ROOT/'art/preview';out.mkdir(exist_ok=True)
font_path=Path('C:/Windows/Fonts/msyh.ttc');font=ImageFont.truetype(str(font_path),23);small=ImageFont.truetype(str(font_path),16)
titles={'Pickup':'接入吊篮：手臂、躯干随原版拾取动作变化','Carry':'载人返回：人物保留原版搬运动作','Place':'回站交接：转向后展开身体、放上病床','Settle':'放稳伤员：头部衔接后逐步过渡到病床姿态','Bed':'站内急救：播放原版病床身体动作','Exit':'治疗结束：起身、离床，末端才释放真实人物','EmergencyExit':'手动紧急释放：保持失能姿态，不免费治疗或起身'}
def render(scene):
    im=Image.new('RGBA',(1080,600),'#1a292c');draw=ImageDraw.Draw(im);origin=(430,535);pixels=115
    draw.line((20,535,1060,535),fill='#52635c',width=2)
    title='出口受阻：保持托管，反向回到病床' if scene.get('returning') else titles[scene['stage']]
    draw.text((24,18),title,font=font,fill='#eef1dc')
    draw.text((24,55),'本机原版默认人物外观 · 生产选帧与坐标求解 · 离线资源预览',font=small,fill='#c0cbb9')
    if scene['flipped']:draw.text((780,55),'含镜像人物转向',font=small,fill='#f8be9d')
    row=scene['row'];p=row['progress'];clip=scene['station_clip'];sf=min(len(station.clips[clip]['frames'])-1,int(p*len(station.clips[clip]['frames'])))
    df=min(len(drone.clips[scene['mechanical']]['frames'])-1,int(p*len(drone.clips[scene['mechanical']]['frames'])))
    station.draw(im,clip,sf,(0,0),.005,origin,pixels)
    drone.draw(im,scene['mechanical'],df,scene['drone_position'],.005*scene['ratio'],origin,pixels)
    pose=solutions[scene['id']];asset=medical if row['medical'] else incap
    opacity=row['emergencyBlend'] if scene['stage']=='EmergencyExit' else 1
    if scene['stage']=='EmergencyExit' and opacity<1:
        source=solutions['0-Bed-40'];medical.draw(im,'working_loop',38,(source['X'],source['Y']),source['Scale'],origin,pixels,source['Horizontal'],patient=True,opacity=1-opacity)
    asset.draw(im,row['clip'],row['frame'],(pose['X'],pose['Y']),pose['Scale'],origin,pixels,pose['Horizontal'],patient=True,rotation=pose['Rotation'],linear=(pose['M00'],pose['M01'],pose['M10'],pose['M11']) if pose['Affine'] else None,opacity=opacity)
    station.draw(im,'fg_'+clip,sf,(0,0),.005,origin,pixels)
    drone.draw(im,'fg_'+scene['mechanical'],df,scene['drone_position'],.005*scene['ratio'],origin,pixels)
    draw.text((24,566),f"{row['file']} / {row['clip']}   帧 {row['frame']+1}/{row['count']}   阶段 {round(p*100)}%",font=small,fill='#c0cbb9')
    return im

all_images={}; durations={}; contact=[]
for flipped in (False,True):
    images=[];times=[]
    for scene in scenes:
        if scene['flipped']!=flipped or scene['stage']=='EmergencyExit':continue
        image=render(scene);images.append(image.convert('RGB'))
        duration=3.75/120 if scene['stage']=='Bed' else 1.8/60 if scene['stage']=='Exit' else 1.2/40
        times.append(max(10,round(duration*100)*10))
        if not flipped and scene['stage'] in ('Pickup','Place','Settle','Bed','Exit') and scene['index'] in (0,60,120):contact.append(image)
    palette=images[0].quantize(colors=192,method=Image.Quantize.MEDIANCUT)
    indexed=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in images]
    filename='watcher-human-mirrored.gif' if flipped else 'watcher-human-cycle.gif'
    indexed[0].save(out/filename,save_all=True,append_images=indexed[1:],duration=times,loop=0,disposal=1,optimize=True)
    print('EXPORTED',filename,len(images),'native human frames',flush=True)
sheet=Image.new('RGB',(1620,1500),'#1a292c')
for i,im in enumerate(contact):
    im=im.convert('RGB');im.thumbnail((540,300),Image.Resampling.LANCZOS);sheet.paste(im,((i%3)*540,(i//3)*300))
sheet.save(out/'watcher-human-contact.png')
emergency=[render(s).convert('RGB') for s in scenes if s['stage']=='EmergencyExit']
emergency[0].save(out/'watcher-human-emergency.gif',save_all=True,append_images=emergency[1:],duration=30,loop=0,optimize=True)
for stage,name,bed_index in (('Exit','watcher-human-return.gif',0),('EmergencyExit','watcher-human-emergency-return.gif',40)):
    forward=[s for s in scenes if s['stage']==stage and not s['flipped'] and s['index']<=78]
    ending=next(s for s in scenes if s['stage']=='Bed' and not s['flipped'] and s['index']==bed_index)
    sequence=forward+[dict(s,returning=True) for s in reversed(forward)]+[ending]*8
    images=[render(s).convert('RGB') for s in sequence]
    images[0].save(out/name,save_all=True,append_images=images[1:],duration=30,loop=0,optimize=True)

def relative_transforms(asset,clip):
    signatures=[]
    for frame in range(len(asset.clips[clip]['frames'])):
        elements={asset.names.get(e['symbol']):e for e in asset.clips[clip]['frames'][frame]}
        a,b,c,d,e,f=elements['torso']['matrix'];det=a*e-b*d
        head=elements['snapto_headshape']['matrix'];x,y=head[2]-c,head[5]-f
        signatures.append((round((e*x-b*y)/det,3),round((-d*x+a*y)/det,3),round(math.atan2(head[3],head[0])-math.atan2(d,a),3)))
    return len(set(signatures))
motion={clip:relative_transforms(asset,clip) for asset,clip in ((incap,'pickup'),(incap,'carry_loop'),(incap,'place'),(medical,'working_pre'),(medical,'working_loop'),(medical,'working_pst'))}
assert all(value>1 for value in motion.values()),motion
record=dict(source='Actual installed-game ANIM/BILD matrices, local native default sprites, production C# frame sampler and placement solver',unity_started=False,native_relative_head_to_torso_transforms=motion,
            production_sources={str(p.relative_to(ROOT)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in (ROOT/'src/Rescue/WatcherInteraction.cs',ROOT/'src/ChamberPatientInteraction.cs',ROOT/'src/Rescue/WatcherVisual.cs',ROOT/'src/Rescue/WatcherStation.cs',ROOT/'src/Rescue/WatcherConfig.cs',ROOT/'src/Rescue/WatcherAnimation.cs')},
            head_quad_seam_maximum_world_error=seams,affine_determinants=affine_determinants,
            scale=dict(raw_units_per_scml_pixel=2,world_units_per_raw_unit=.005,station_width_m=4,drone_width_m=1.8),native_assets={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (recon/'assets/body_comp_default_build.bytes',recon/'assets/anim_healing_bed_anim.bytes',recon/'assets/anim_incapacitated_anim.bytes')})
(out/'watcher-human-evidence.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS articulated motion after torso registration:',motion)
