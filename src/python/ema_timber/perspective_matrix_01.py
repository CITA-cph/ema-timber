import numpy as np
import SimpleITK as sitk

import matplotlib.pyplot as plt

def fucking_around():
    world_points = np.float32([
        [0,0,0],  
        [1, 0, 0], 
        [1, 1, 0], 
        [0,1, 0]])

    image_points = np.float32([
        [482.32004,  73.36037, 0],
        [934.1217,   69.12398, 0],
        [937.88257, 521.02124, 0],
        [486.0776,  524.55707, 0]])

    #image_points = np.transpose(image_points)

    H = world_points @ image_points
    HT = np.transpose(H)

    persp_matrix = np.float32([
        [2.22029708e-03, -1.84906451e-05, -1.06953729e+00],
        [ 2.08144437e-05,  2.21981504e-03, -1.72885669e-01],
        [ 3.44183964e-06, -4.11160988e-08,  1.00000000e+00]])


    U, S, Vh = np.linalg.svd(HT)

    R = U @ Vh

    print(R.shape)
    print(R)

def fill(img, color):
    for x in range(img.GetWidth()):
        for y in range(img.GetHeight()):
            img.SetPixel(x, y, color)

def resample(image, transform):
    # Output image Origin, Spacing, Size, Direction are taken from the reference
    # image in this call to Resample
    reference_image = image
    interpolator = sitk.sitkCosineWindowedSinc
    default_value = 100.0
    return sitk.Resample(image, reference_image, transform, interpolator, default_value)

def main():
    canvas = sitk.Image([200,400], sitk.sitkVectorFloat32, 3)
    canvas.SetSpacing((1,1))

    red = sitk.Image([200,200], sitk.sitkVectorFloat32, 3)
    red.SetSpacing((1,1))
    fill(red, (1,0,0))

    red = sitk.Resample(red, canvas)

    translation = sitk.TranslationTransform(2, (0,-200))
    rotation = sitk.Euler2DTransform((100,100), 0.9)

    # projection = sitk.Transform()
    # projection.SetMatrix([
    #     [2.22029708e-03, -1.84906451e-05, -1.06953729e+00],
    #     [ 2.08144437e-05,  2.21981504e-03, -1.72885669e-01],
    #     [ 3.44183964e-06, -4.11160988e-08,  1.00000000e+00]])


    composite = sitk.CompositeTransform([rotation, translation])

    blue = sitk.Image([200,200], sitk.sitkVectorFloat32, 3)
    blue.SetSpacing((1,1))
    blue.SetOrigin((0,200))
    blue.SetDirection(sitk.Euler2DTransform((0,0), 0.1).GetMatrix())

    fill(blue, (0,0,1))

    #blue = sitk.Resample(blue, canvas, translation, sitk.sitkLinear)
    # blue = sitk.Resample(blue, canvas, projection, sitk.sitkLinear)
    blue = sitk.Resample(blue, canvas)


    canvas = canvas + blue + red

    for d in dir(sitk):
        if "Overlay" in d:
            print(d)



    plt.imshow(sitk.GetArrayViewFromImage(canvas))
    plt.axis("off");

    plt.show()
    #print(canvas)

    pass

if __name__=="__main__":
    main()