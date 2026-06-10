# /// script
# requires-python = ">=3.11"
# dependencies = [
#     "pillow",
# ]
# ///

from pathlib import Path
from PIL import Image

work_dir = Path(__file__).resolve().parent.parent

def paste(img,str,h,v,x,y):
    block=Image.open(work_dir / "workplace" / str)
    if h:
        block=block.transpose(Image.FLIP_LEFT_RIGHT)
    if v:
        block=block.transpose(Image.FLIP_TOP_BOTTOM)
    img.paste(block, (x*8,y*8))

def tp(v,h):
    if v and h:
        return "2"
    elif (not v) and (not h):
        return "0"
    elif v:
        return "v"
    else:
        return "h"

for i in range(16):
    img = Image.new('RGBA', (32, 32))
    paste(img,f"sidev{i>>1&1}.png",0,0,1,0)
    paste(img,f"sidev{i>>3&1}.png",0,1,1,3)
    paste(img,f"sideh{i>>2&1}.png",0,0,0,1)
    paste(img,f"sideh{i>>0&1}.png",1,0,3,1)
    paste(img,f"corner{tp(i>>1&1,i>>2&1)}.png",0,0,0,0)
    paste(img,f"corner{tp(i>>1&1,i>>0&1)}.png",1,0,3,0)
    paste(img,f"corner{tp(i>>3&1,i>>2&1)}.png",0,1,0,3)
    paste(img,f"corner{tp(i>>3&1,i>>0&1)}.png",1,1,3,3)

    img.save(work_dir / f"block{i:02d}.png")