import glob
import os
import cv2

def naming(i):
    if i <100:
        if i <10:
            i = "00"+str(i)
        else:
            i = "0"+str(i)
    else:
        i = str(i)
    return i 

def getPaths(parentpath):
    dir = os.path.join(parentpath, "*")
    path_ls = glob.glob(dir)
    return path_ls

def makeDir(i, parentpath):
        dir = os.path.join(parentpath, "{}".format(i))
        if not os.path.exists(dir):
            os.makedirs(dir)
        return dir

def getImgs(parentpath):
    img_ls = []
    dir = os.path.join(parentpath, "*.png")
    path_ls = glob.glob(dir)
    for i in path_ls:
        print (i)
        img = cv2.imread(i)
        img_ls.append(img)
    return img_ls

def writeImgs(img,parentpath,dir = "", id = ""):
    t_dir = makeDir(dir, parentpath)
    #print (type(img))
    if type(img) is list  :
        for i in range(len(img)):
            a = naming(i)
            i_dir = os.path.join(t_dir, f"{id}_{a}.png")
            cv2.imwrite(i_dir, img[i])
       
    else:
        i_dir = os.path.join(t_dir, f"{id}.png")
        cv2.imwrite(i_dir, img)

def datatoTxt(k_ls, parentpath,  dir="", id ="", extra = ""):
    t_dir = makeDir(dir, parentpath)
    line = ""
    for i in k_ls:
        line += " "
        for j in i:
            line += "{}:".format(j)
        line = line[:-1]
    line = line[1:]

    with open(t_dir + id + extra+ ".txt", "w") as f:
        f.write(line)
        f.close()