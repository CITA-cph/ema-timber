import numpy as np
import cv2
import os
import glob
import sys

dir1 = os.path.dirname(os.path.abspath(__file__))
sys.path.append(os.path.dirname(dir1))

for f in glob.glob("img_array/*.npy"):
    data = np.load(f)
    im = data.reshape(1944,2592,3)

    im= cv2.cvtColor(im, cv2.COLOR_RGB2BGR)
    cv2.imwrite("images/"+f[:-4]+".png",im)
    cv2.imshow('', im)
    cv2.waitKey()
    cv2.destroyAllWindows()