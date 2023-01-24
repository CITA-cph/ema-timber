import cv2
import  numpy as np

def getmtx(path):
    with open(path) as f:
        lines = f.readlines()
    chunks =lines[0].split(" ")
    value_ls = []
    for v in chunks:
        value_ls.append(float(v))
    mtx = np.array([value_ls[:3],value_ls[3:6],value_ls[6:]])
    return mtx

def getdist(path):
    with open(path) as f:
        lines = f.readlines()
    chunks =lines[0].split(" ")
    value_ls = []
    for v in chunks:
        value_ls.append(float(v))
    dist= np.array(value_ls)
    return dist

def corDist(img, mtx, dist):

    h,  w = img.shape[:2]
    newCameraMatrix, roi = cv2.getOptimalNewCameraMatrix(mtx, dist, (w,h), 1, (w,h))

    # Undistort
    uncropped = cv2.undistort(img, mtx, dist, None, newCameraMatrix)

    # crop the image
    x, y, w, h = roi
    cropped = uncropped[y:y+h, x:x+w]
    return cropped

def cropimg(img, pt_ls):
    top_l = pt_ls[0]
    bot_r = pt_ls[1]
    img2 = img.copy()
    img2 = img2[top_l[0]:bot_r[0],top_l[1]:bot_r[1],]
    
    return img2

def stitch(img,img2):
    
    new_img = np.concatenate((img,img2),axis=0)
    return new_img

def fix_img (img_ls, mtx , dist, trans, cropls):
    new_ls = []
    try:
        for i in range(len(img_ls)):
            trans_m = np.array([[1,0,trans*i*-1],[0,1,0]]).astype("float32") #Move +ve Y
            dist_fix = corDist(img_ls[i], mtx, dist)
            
            if i == 0:
                cropped = cropimg(dist_fix, [(0,cropls[0][1]),cropls[1]])
                
            else:
                cropped = cropimg(dist_fix, cropls)

            translated = cv2.warpAffine(cropped,trans_m,(cropped.shape[1], cropped.shape[0]))
            new_ls.append(translated)
        stitched = new_ls[0]
        
        for j in range(1,len(new_ls)):
            stitched = stitch(stitched,new_ls[j])
    except Exception as e:
        print (e)
    return stitched
