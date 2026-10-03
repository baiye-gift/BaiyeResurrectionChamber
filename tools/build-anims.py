"""Compile untouched housing and original sci-fi mechanical layers into KAnim.

Patient appearance is rendered from native game data at runtime. There is no
patient silhouette in this asset. Hidden layers are omitted from mainline keys;
opacity is baked into PNG variants rather than relying on SCML alpha support.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import argparse, math, os, subprocess, xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
NAME='baiye_reconstruction'
SOURCE=ROOT/'art/source'/NAME
DEST=ROOT/'anim/assets'/NAME

def build(kanimal):
    SOURCE.mkdir(parents=True,exist_ok=True);DEST.mkdir(parents=True,exist_ok=True)
    body=Image.open(ROOT/'art/source/chamber-housing-chairless.png').convert('RGBA')
    body.thumbnail((800,800),Image.Resampling.LANCZOS)
    icon=Image.new('RGBA',(128,128));small=body.copy();small.thumbnail((116,116),Image.Resampling.LANCZOS);icon.alpha_composite(small,((128-small.width)//2,(128-small.height)//2))
    sprites=[('body_0',body,(.5,0)),('patient_anchor_0',Image.new('RGBA',(1,1)),(.5,.5)),('ui_0',icon,(.5,.5))]
    def add(name,im,pivot=(.5,.5),fades=False):
        # PNG opacity survives conversion even if the converter ignores SCML a.
        for n in range(8 if fades else 1):
            frame=im.copy()
            if fades:frame.putalpha(frame.getchannel('A').point(lambda a:round(a*(n+1)/8)))
            sprites.append((name+'_'+str(n),frame,pivot))
    palette={'blue':(128,143,255),'green':(112,238,187),'amber':(255,190,76),'red':(255,112,94)}
    for name,rgb in palette.items():
        ring=Image.new('RGBA',(120,32));d=ImageDraw.Draw(ring)
        d.ellipse((4,5,115,26),outline=rgb+(38,),width=8)
        d.arc((4,5,115,26),8,171,fill=rgb+(145,),width=2)
        d.arc((4,5,115,26),188,351,fill=(222,247,255,190),width=2)
        for x in (4,112):d.rectangle((x,13,x+4,18),fill=rgb+(200,))
        add('ring_'+name,ring,fades=True)
        scanner=Image.new('RGBA',(118,12));d=ImageDraw.Draw(scanner)
        d.rounded_rectangle((2,1,115,10),radius=4,fill=rgb+(24,))
        d.line((3,6,114,6),fill=rgb+(130,),width=2);add('scanner_'+name,scanner)
        dot=Image.new('RGBA',(8,8));d=ImageDraw.Draw(dot)
        d.ellipse((0,0,7,7),fill=rgb+(35,));d.ellipse((2,2,5,5),fill=rgb+(170,));add('particle_'+name,dot,fades=True)
        light=Image.new('RGBA',(16,12));ImageDraw.Draw(light).rounded_rectangle((0,0,15,11),radius=3,fill=rgb+(230,),outline=(26,47,49,230),width=2);add('light_'+name,light,fades=True)
        pulse=Image.new('RGBA',(38,20));ImageDraw.Draw(pulse).line([(0,10),(9,10),(13,3),(18,18),(23,7),(28,10),(37,10)],fill=rgb+(220,),width=2);add('pulse_'+name,pulse,fades=True)
    rail=Image.new('RGBA',(7,156));d=ImageDraw.Draw(rail)
    d.rounded_rectangle((0,0,6,155),radius=3,fill=(142,139,255,28));d.line((3,5,3,150),fill=(165,182,255,85),width=1);add('rail',rail)
    flow=Image.new('RGBA',(5,15));d=ImageDraw.Draw(flow);d.line((2,0,2,14),fill=(180,255,243,140),width=2);add('flow',flow)
    bubble=Image.new('RGBA',(6,6));ImageDraw.Draw(bubble).ellipse((0,0,5,5),outline=(194,255,240,130),width=1);add('bubble',bubble)
    # Newly authored independent transparent overlays, not retouched housing.
    glass=Image.new('RGBA',(128,246));d=ImageDraw.Draw(glass)
    d.rounded_rectangle((2,2,125,243),radius=27,fill=(166,235,238,22),outline=(164,227,229,185),width=3)
    d.line((14,38,14,200),fill=(223,255,255,72),width=2)
    d.arc((10,9,120,69),192,300,fill=(222,255,255,95),width=3)
    add('door',glass,pivot=(.5,1))
    fluid=Image.new('RGBA',(116,240));d=ImageDraw.Draw(fluid)
    d.rounded_rectangle((0,0,115,239),radius=18,fill=(83,221,199,65))
    d.line([(4,10),(21,7),(39,10),(58,8),(78,11),(97,8),(112,10)],fill=(206,255,236,140),width=2)
    add('fluid',fluid,pivot=(.5,0))
    root=ET.Element('spriter_data',scml_version='1.0',generator='BaiyeResurrectionChamber',generator_version='0.1.3')
    folder=ET.SubElement(root,'folder',id='0');ids={}
    for i,(name,im,pivot) in enumerate(sprites):
        im.save(SOURCE/(name+'.png'));ids[name]=i
        ET.SubElement(folder,'file',id=str(i),name=name+'.png',width=str(im.width),height=str(im.height),pivot_x=str(pivot[0]),pivot_y=str(pivot[1]))
    entity=ET.SubElement(root,'entity',id='0',name=NAME)
    stages=['idle','loaded','reconstruct','recover','paused','alarm','release']
    kinds=stages+['off','on','place','working_pre','working_loop','working_pst','ui']+['fx_'+s for s in stages]+['pod_door','pod_fluid']
    for n,kind in enumerate(kinds):
        fx=kind.startswith('fx_');stage=kind[3:] if fx else kind
        if stage in ('working_pre','working_loop'):stage='reconstruct'
        if stage=='working_pst':stage='release'
        count=51 if kind.startswith('pod_') else 1 if stage in ('idle','off','on','place','ui','paused','alarm') else 72 if stage=='loaded' else 12 if stage=='release' else 36
        anim=ET.SubElement(entity,'animation',id=str(n),name=kind,length=str(count*100),interval='100',looping='true')
        main=ET.SubElement(anim,'mainline');timelines={}
        for i in range(count):
            t=i/count;active=stage in ('reconstruct','recover')
            color={'reconstruct':'blue','recover':'green','release':'green','loaded':'amber','paused':'amber','alarm':'red'}.get(stage)
            frames=[('patient_anchor','patient_anchor_0',dict(x=-1,y=225))]
            def layer(timeline,sprite,**attrs):frames.append((timeline,sprite,attrs))
            strength=min(7,int(3+4*math.sin(math.pi*t)**2))
            if not fx:
                frames.insert(0,('body','body_0',dict(x=0,y=0,scale_x=.512,scale_y=.512)))
                if color:layer('light','light_'+color+'_'+str(strength if active else 2),x=0,y=375)
                if active:
                    for j in range(4):
                        u=(t+j/4)%1
                        layer('bubble'+str(j),'bubble_0',x=(-130 if j<2 else 139)+j%2*6,y=(191 if j<2 else 140)+u*65)
                        layer('supply'+str(j),'flow_0',x=-138 if j<2 else 137,y=(160 if j<2 else 125)+u*64)
            else:
                if color:layer('pulse','pulse_'+color+'_'+str(strength if active else 1),x=147,y=116,scale_x=.8,scale_y=.8)
                if stage=='loaded':layer('scanner','scanner_amber_0',x=-1,y=224+62*math.sin(2*math.pi*t))
                if active:
                    layer('scanner','scanner_'+color+'_0',x=-1,y=224+62*math.sin(2*math.pi*t))
                    for j in range(2 if stage=='reconstruct' else 1):
                        u=(t+j/2)%1
                        layer('ring'+str(j),'ring_'+color+'_'+str(strength),x=-1,y=166+126*u,scale_y=.8)
                    if stage=='reconstruct':
                        for j,x in enumerate((-55,53)):layer('rail'+str(j),'rail_0',x=x,y=225)
                        for j in range(10):
                            u=(t+j/10)%1;side=-1 if j%2==0 else 1
                            layer('particle'+str(j),'particle_blue_'+str(min(7,int(7*math.sin(math.pi*u)))),x=-1+side*(52-40*u),y=164+(j%5)*29+9*math.sin(2*math.pi*u))
                if stage=='release':
                    fade=max(0,7-int(t*8))
                    layer('ring0','ring_green_'+str(fade),x=-1,y=220,scale_x=1-.4*t,scale_y=1-.4*t)
            if kind=='ui':frames=[('ui','ui_0',dict(x=0,y=0))]
            if kind.startswith('pod_'):
                frames=[('patient_anchor','patient_anchor_0',dict(x=-1,y=225))]
                if i>0:
                    if kind=='pod_door':layer('door','door_0',x=-1,y=332,scale_y=i/50)
                    else:layer('fluid','fluid_0',x=-1,y=84,scale_y=i/50)
            key=ET.SubElement(main,'key',id=str(i),time=str(i*100))
            for z,(timeline,sprite,attrs) in enumerate(frames):
                if timeline not in timelines:timelines[timeline]=ET.SubElement(anim,'timeline',id=str(len(timelines)),name=timeline)
                tl=timelines[timeline];key_id=str(len(tl.findall('key')));tk=ET.SubElement(tl,'key',id=key_id,time=str(i*100))
                ET.SubElement(tk,'object',folder='0',file=str(ids[sprite]),**{k:str(round(v,6)) for k,v in attrs.items()})
                ET.SubElement(key,'object_ref',id=str(z),timeline=tl.attrib['id'],key=key_id,z_index=str(z))
    ET.indent(root);scml=SOURCE/(NAME+'.scml');ET.ElementTree(root).write(scml,encoding='utf-8',xml_declaration=True)
    env=os.environ.copy();env['DOTNET_ROLL_FORWARD']='Major'
    subprocess.run([kanimal,'kanim',str(scml),'-o',str(DEST)],env=env,check=True)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--kanimal',required=True);build(p.parse_args().kanimal)
