import sys
import SimpleITK as sitk

import matplotlib.pyplot as plt

grid_image = sitk.GridSource(outputPixelType=sitk.sitkUInt16, size=(512,512),
                             sigma=(0.1,0.1), gridSpacing=(20.0,20.0))

img = sitk.GetArrayFromImage(grid_image)

def onclick(event): 
    print(dir(event))
    print(event.inaxes.get_figure())
    print("button=%d, x=%d, y=%d, xdata=%f, ydata=%f" % ( 
        event.button, event.x, event.y, event.xdata, event.ydata))
     
    event.inaxes.add_patch(plt.Circle((event.xdata, event.ydata), 0.1, color='r'))

ax = plt.imshow(img)

fig = ax.get_figure()
cid = fig.canvas.mpl_connect('button_press_event', onclick) 

plt.show()


sys.exit( 0 )