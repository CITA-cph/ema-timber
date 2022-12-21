import init_examples
import os 

base_dir = os.path.dirname(os.path.abspath(__file__))
print (base_dir)

from ema_timber.process.knotscrapper import Board
from ema_timber.process.knotscrapper import prepFile
from ema_timber.process.knotscrapper import editImg
from ema_timber.process.knotscrapper import numtoimage as ni


#-+-+-+-+-+-#

date = "221220"
np_array = "np_array"
raw_img = "raw_img"
post_img = "post_img"
looked_img = "looked_img"
knots_img = "knots_img"
knots_pos = "knots_pos"
bound_pos = "bound_pos"

#-+-+-+-+-+-#

mtx = editImg.getmtx(os.path.join(base_dir , "calibmatrix.txt"))
dist = editImg.getdist(os.path.join(base_dir , "dist.txt"))
trans = 0
cropls = [(378, 0), (882, 2404)] 

#-+-+-+-+-+-#

Board_ls = [] # List of Boards

np_array_paths = prepFile.getPaths(os.path.join(base_dir, date ,np_array))
for i in range(len(np_array_paths)):
        id = prepFile.naming(i)
        imgs = ni.makeImgs(np_array_paths[i])
        prepFile.writeImgs(imgs,os.path.join(base_dir,date,raw_img), dir= id , id = id )



raw_img_paths = prepFile.getPaths(os.path.join(base_dir, date ,raw_img)) # Paths to raw IMGS

for i in range(len(raw_img_paths)): # Create class object board
    id = prepFile.naming(i)
    imgs = prepFile.getImgs(raw_img_paths[i])

    a = Board.Board(id, imgs, "RAW")
    print ("{} - created".format(id))
    Board_ls.append(a)
    

for b in Board_ls: # Fix and Stitch IMG
    
    try:
        stitch = editImg.fix_img(b.imgs[0] , mtx, dist, trans, cropls)
        b.imgs.append(stitch)
        b.status = "STITCHED"
        print ("{} - {}".format(b.id, b.status))
    except:

        print("{}.png could not be created".format(b.id))
        raise()
    
    # Prepare image
    b.contrastImg(2.2, (45,15))
    b.getContours(75,100)
    b.getKeyBlobs(True,True,True,1000,0.1,0.1)
    b.cropKnots()
    
    # Scan for knots and show
    b.scan(show = False, bounds = True , knots = True , resize = True, s = 0.2)

    # Save imgs
    prepFile.writeImgs(b.imgs[1],os.path.join(base_dir, date ,post_img), dir= "", id = b.id ) # Save Stitched img
    prepFile.writeImgs(b.imgs[2],os.path.join(base_dir, date ,knots_img), dir= b.id, id = b.id ) # Save Knots img
    prepFile.writeImgs(b.imgs[3],os.path.join(base_dir, date ,looked_img), dir= "", id = b.id ) # Save Scanned img

    # Save TXT
    prepFile.datatoTxt(b.knots[1], os.path.join(base_dir, date ,knots_pos), dir= "", id = b.id, extra = "_kpos") # Save Knots pos to Txt
    prepFile.datatoTxt(b.bound[1], os.path.join(base_dir, date ,bound_pos), dir= "", id = b.id, extra = "_bpos") # Save Bounds pos to Txt