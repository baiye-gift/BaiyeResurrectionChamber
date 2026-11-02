"""Read native ANIM/BILD geometry for offline evidence; never imports Unity.

Sprite PNGs come from Kanimal's local native decode. Raw build dimensions and
frame matrices, not PNG dimensions or an assumed world scale, place each quad.
"""
from pathlib import Path
import math
import struct
from functools import lru_cache
from PIL import Image


class Reader:
    def __init__(self, path): self.data = Path(path).read_bytes(); self.offset = 4
    def take(self, fmt):
        result = struct.unpack_from('<'+fmt, self.data, self.offset)
        self.offset += struct.calcsize('<'+fmt)
        return result[0] if len(result) == 1 else result
    def string(self):
        size = self.take('i'); result = self.data[self.offset:self.offset+size].decode('utf-8'); self.offset += size
        return result
    def hashes(self):
        result = {}
        for _ in range(self.take('i')):
            key = self.take('i'); result[key] = self.string()
        return result


class NativeAsset:
    def __init__(self, build, anim, sprites):
        self.sprites = Path(sprites); self.symbols = {}; self.clips = {}
        r = Reader(build); assert r.data[:4] == b'BILD'
        version, count, _ = r.take('3i'); assert version in (9, 10)
        self.name = r.string()
        for _ in range(count):
            key = r.take('i')
            if version == 10: r.take('i')
            _, _, frames = r.take('3i'); values = []
            for _ in range(frames):
                index, duration, texture, x, y, width, height, u0, v0, u1, v1 = r.take('3i8f')
                values.append(dict(index=index, duration=duration, box=(x-width/2, y-height/2, x+width/2, y+height/2)))
            self.symbols[key] = values
        self.names = r.hashes()
        r = Reader(anim); assert r.data[:4] == b'ANIM'
        version, _, _, count = r.take('4i'); assert version == 5
        for _ in range(count):
            name = r.string(); root, fps, frames = r.take('ifi'); values = []
            for _ in range(frames):
                r.take('4f'); elements = []
                for _ in range(r.take('i')):
                    key, index, _, _, *v = r.take('4i11f')
                    elements.append(dict(symbol=key, index=index, alpha=v[0]*v[3], matrix=(v[4], v[6], v[8], v[5], v[7], v[9])))
                values.append(elements)
            self.clips[name] = dict(fps=fps, frames=values)
        r.take('i'); self.names.update(r.hashes())

    def elements(self, clip, frame, patient=False):
        # Native draw order is front-to-back, SCML is back-to-front.
        for item in reversed(self.clips[clip]['frames'][frame]):
            name = self.names.get(item['symbol'], '')
            if patient and name.lower() in HIDDEN: continue
            if item['alpha'] <= 0 or item['symbol'] not in self.symbols: continue
            options = self.symbols[item['symbol']]
            symbol = next((f for f in options if f['index'] <= item['index'] < f['index']+f['duration']), None)
            if symbol is None: continue
            yield name, symbol, item

    @lru_cache(maxsize=800)
    def geometry(self, clip, frame):
        points = []
        for name, symbol, item in self.elements(clip, frame, True):
            a,b,c,d,e,f = item['matrix']; x0,y0,x1,y1 = symbol['box']
            points.extend((a*x+b*y+c, -(d*x+e*y+f)) for x,y in ((x0,y0),(x0,y1),(x1,y0),(x1,y1)))
        assert points, (clip,frame)
        xs, ys = zip(*points)
        _,symbol,item=next((n,s,e) for n,s,e in self.elements(clip,frame,True) if n=='snapto_headshape');head=item['matrix'];x0,y0,x1,y1=symbol['box'];cx,cy=(x0+x1)/2,(y0+y1)/2
        return dict(X=(min(xs)+max(xs))/2, Y=(min(ys)+max(ys))/2, Width=max(xs)-min(xs), Height=max(ys)-min(ys),HeadX=head[0]*cx+head[1]*cy+head[2],HeadY=-(head[3]*cx+head[4]*cy+head[5]),HeadDX=head[0]*(x1-x0),HeadDY=-head[3]*(x1-x0),HeadEX=head[1]*(y1-y0),HeadEY=-head[4]*(y1-y0))

    @lru_cache(maxsize=700)
    def sprite(self, name, index):
        path = self.sprites/(name+'_'+str(index)+'.png')
        if not path.exists():
            path = next((p for p in self.sprites.glob('*.png') if p.name.lower()==path.name.lower()), path)
        return Image.open(path).convert('RGBA')

    def draw(self, canvas, clip, frame, position, scale, screen_origin, pixels=100, horizontal=1, patient=False,rotation=0,linear=None,opacity=1):
        if opacity<=0:return
        cosine,sine=math.cos(math.radians(rotation)),math.sin(math.radians(rotation))
        for name, symbol, item in self.elements(clip, frame, patient):
            sprite = self.sprite(name, symbol['index'])
            if not sprite.getchannel('A').getbbox(): continue
            a,b,c,d,e,f = item['matrix']; x0,y0,x1,y1 = symbol['box']
            if linear:
                l00,l01,l10,l11=linear
                a,b,c,d,e,f=(l00*a-l01*d,l00*b-l01*e,l00*c-l01*f,-l10*a+l11*d,-l10*b+l11*e,-l10*c+l11*f);scale=1
            else:
                a,b,c,d,e,f=(cosine*horizontal*a+sine*d,cosine*horizontal*b+sine*e,cosine*horizontal*c+sine*f,-sine*horizontal*a+cosine*d,-sine*horizontal*b+cosine*e,-sine*horizontal*c+cosine*f)
            sx, sy = (x1-x0)/sprite.width, (y1-y0)/sprite.height
            # Screen Y is down; both native and screen axes are therefore down.
            matrix = (a*sx*scale*pixels, b*sy*scale*pixels,
                      screen_origin[0]+(position[0]+(a*x0+b*y0+c)*scale)*pixels,
                      d*sx*scale*pixels, e*sy*scale*pixels,
                      screen_origin[1]-(position[1]-(d*x0+e*y0+f)*scale)*pixels)
            draw_quad(canvas, sprite, matrix, item['alpha']*opacity)


HIDDEN = {'cast','snapto_hat','snapto_hat_hair','snapto_headfx','snapto_chest','snapto_neck',
          'snapto_goggles','snapto_pivot','snapto_rgthand','necklace','skirt'}


def draw_quad(canvas, sprite, matrix, alpha=1):
    a,b,c,d,e,f = matrix
    corners = [(a*x+b*y+c,d*x+e*y+f) for x,y in ((0,0),(sprite.width,0),(0,sprite.height),sprite.size)]
    xs,ys = zip(*corners); left,top = math.floor(min(xs))-2,math.floor(min(ys))-2
    width,height = math.ceil(max(xs))-left+2,math.ceil(max(ys))-top+2
    if width<=0 or height<=0 or left>=canvas.width or top>=canvas.height or left+width<0 or top+height<0:return
    determinant=a*e-b*d
    if abs(determinant)<1e-12:return
    c-=left;f-=top
    inverse=(e/determinant,-b/determinant,(b*f-e*c)/determinant,-d/determinant,a/determinant,(d*c-a*f)/determinant)
    layer=sprite.transform((width,height),Image.Transform.AFFINE,inverse,Image.Resampling.BICUBIC)
    if alpha<1:layer.putalpha(layer.getchannel('A').point(lambda p:round(p*max(0,alpha))))
    canvas.alpha_composite(layer,(left,top))
