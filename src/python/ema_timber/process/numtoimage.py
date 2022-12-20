import numpy as np
import cv2
import os
import glob
import sys

'''
Add the ema_timber folder to system path
so that we can load ema_timber from our
example scripts.
'''
base_dir =  os.path.abspath(os.path.join(__file__ ,"../../../../.."))

for f in glob.glob(base_dir+"\\examples\\img_array\\*.npy"):
    
    data = np.load(f)
    im = data.reshape(1944,2592,3)
    im= cv2.cvtColor(im, cv2.COLOR_RGB2BGR)
    print(base_dir + "\\examples\\final_img\\"+os.path.basename(f)[:-4]+".png")
    cv2.imwrite(base_dir + "\\examples\\final_img\\"+os.path.basename(f)[:-4]+".png",im)