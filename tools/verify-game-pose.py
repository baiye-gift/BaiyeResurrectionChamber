"""Read local native TextAsset motion metadata; never export game art.

Checks the pod idle and carried medical pose used by the runtime adapter in the installed
game. This does not run Unity, render accessories, or simulate a carrier.
"""
import argparse,mmap,struct
from pathlib import Path

def verify(path,name,clip_name,upright):
    with path.open('rb') as f,mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ) as asset:
        pos=asset.find(name)
        assert pos>=4 and struct.unpack_from('<I',asset,pos-4)[0]==len(name),'medical motion asset unavailable'
        pos=(pos+len(name)+3)&~3
        length=struct.unpack_from('<I',asset,pos)[0];data=asset[pos+4:pos+4+length]
    assert data[:4]==b'ANIM';offset=4
    def read(fmt):
        nonlocal offset
        result=struct.unpack_from('<'+fmt,data,offset);offset+=struct.calcsize('<'+fmt);return result
    def string():
        nonlocal offset
        n=read('I')[0];s=data[offset:offset+n].decode('utf-8');offset+=n;return s
    version,total_elements,total_frames,count=read('4I');assert version==5
    clips={}
    for _ in range(count):
        clip=string();root,rate,frames=read('IfI');first=None
        for j in range(frames):
            bounds=read('4f');n=read('I')[0];elements=[]
            for _ in range(n):
                symbol,frame,folder,unused=read('4I');values=read('11f');elements.append((symbol,frame,values[4:10]))
            if j==0:first=(bounds,elements)
        clips[clip]=(rate,frames,first)
    maximum=read('I')[0];hashes={}
    for _ in range(read('I')[0]):
        h=read('I')[0];hashes[h]=string()
    assert offset==len(data),'motion parse did not consume complete native data'
    assert clip_name in clips,'native pose missing: '+clip_name
    rate,frames,(bounds,elements)=clips[clip_name]
    symbols=[hashes.get(e[0],str(e[0])) for e in elements]
    assert any('head' in s for s in symbols),'medical pose has no head'
    head=next(e[2] for e in elements if hashes.get(e[0])=='snapto_headshape')
    feet=[e[2] for e in elements if hashes.get(e[0])=='foot']
    dx=head[4]-sum(e[4] for e in feet)/len(feet);dy=-head[5]+sum(e[5] for e in feet)/len(feet)
    if upright:
        assert dy>abs(dx),'native pod idle is not upright; inspect head/feet axis'
        print('PASS pod head above feet; native upright pose, no rotation needed')
    else:
        assert abs(dx)>abs(dy),'native carried medical pose is not horizontal'
        print('PASS carried patient retains horizontal medical pose')
    print('PASS actual game '+clip_name+':',frames,'frames at',rate,'fps; first-frame bounds',bounds)
    print('First-frame symbols:',', '.join(symbols))
    print('First-frame head/feet/torso transforms:',[(hashes.get(e[0]),e[1],e[2]) for e in elements if hashes.get(e[0],'') in ('snapto_headshape','foot','torso','cast')])
    print('Pose metadata verified; Unity rendering and carrier navigation unverified.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('assets',type=Path);path=p.parse_args().assets
    verify(path,b'anim_idles_default_anim','idle_default',True)
    verify(path,b'anim_healing_bed_anim','working_loop',False)
