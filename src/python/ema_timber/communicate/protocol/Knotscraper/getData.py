import os 
from . import Board, prepFile, editImg, numtoimage as ni

def run(base_dir, date, filename):

    #-+-+-+-+-+-#
    camera_config = "camera_config"
    np_array = "np_array"
    raw_img = "raw_img"
    post_img = "post_img"
    looked_img = "looked_img"
    knots_img = "knots_img"
    knots_pos = "knots_pos"
    bound_pos = "bound_pos"

    #-+-+-+-+-+-#

    mtx = editImg.getmtx(os.path.join( base_dir , camera_config, "calibmatrix.txt"))
    dist = editImg.getdist(os.path.join(base_dir , camera_config, "dist.txt"))
    trans = 0 # goes to camera_config
    cropls = [(378, 0), (882, 2404)] # goes to camera_config

    #-+-+-+-+-+-#

    np_array_paths = os.path.join(base_dir, date, np_array+"/"+filename)
    imgs = ni.makeImgs(np_array_paths)
    prepFile.writeImgs(imgs,os.path.join(base_dir,date,raw_img), dir= filename , id = filename )



    raw_img_paths = os.path.join(base_dir,date, raw_img+"/"+filename) # Paths to raw IMGS

    imgs = prepFile.getImgs(raw_img_paths)

    a = Board.Board(filename, imgs, "RAW")
    print ("{} - created".format(filename))
        
    try:
        stitch = editImg.fix_img(a.imgs[0] , mtx, dist, trans, cropls)
        a.imgs.append(stitch)
        a.status = "STITCHED"
        print ("{} - {}".format(a.id, a.status))
    except:
        print("{}.png could not be created".format(a.id))
    
    # Prepare image
    a.contrastImg(2.2, (45,15))
    a.getContours(150,250)
    a.getKeyBlobs(True,True,True,1000,0.1,0.1)
    a.cropKnots()
    # Scan for knots and show
    a.scan(show = False, bounds = True , knots = True , resize = True, s = 0.2)
    # Save imgs
    prepFile.writeImgs(a.imgs[1],os.path.join(base_dir,date,post_img), dir= "", id = a.id ) # Save Stitched img
    prepFile.writeImgs(a.imgs[3],os.path.join(base_dir,date,knots_img), dir= a.id, id = a.id ) # Save Knots img
    prepFile.writeImgs(a.imgs[4],os.path.join(base_dir,date,looked_img), dir= "", id = a.id ) # Save Scanned img

    # Save TXT
    prepFile.datatoTxt(a.knots[1], os.path.join(base_dir,date,knots_pos), dir= "", id = a.id, extra = "_kpos") # Save Knots pos to Txt
    prepFile.datatoTxt(a.bound[1], os.path.join(base_dir,date,bound_pos), dir= "", id = a.id, extra = "_bpos") # Save Bounds pos to Txt
    
    return

    
