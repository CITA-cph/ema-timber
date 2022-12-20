import numpy as np
import cv2
import glob

def makeImgs(dir):
    img_ls = []
    path_ls = glob.glob(dir)
    for i in path_ls:
        data = np.load(i)
        im = data.reshape(1944,2592,3)
        im= cv2.cvtColor(im, cv2.COLOR_RGB2BGR)
        img_ls.append(im)
    return img_ls
