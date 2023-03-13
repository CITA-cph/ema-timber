import logging

import rhino3dm
import specklepy
from specklepy.objects.geometry import Brep, Curve

from rhino3dm import Point3d, Point4d, Vector3d
import OCC


def to_rhino_curve(occ_curve):

    degree = 3
    Npts = 10

    rcrv = rhino3dm.NurbsCurve(degree=degree, pointcount=Npts)

    # Set points and weights
    for i in range(Npts):
        rcrv.Points[i] = Point4d(0,0,0,1)

    # Set knots
    for i in range(len(rcrv.Knots)):
        rcrv.Knots[i] = 2.0

    return rcrv

def to_rhino_brep(occ_brep):

    rbrep = rhino3dm.Brep()
    print(dir(rbrep))


def to_speckle_brep(occ_brep):
    """
    Surfaces: List[Surface] = None
    Curve3D: List[Base] = None
    Curve2D: List[Base] = None
    Vertices: List[Point] = None

    Edges: List[BrepEdge] = None
    Loops: List[BrepLoop] = None
    Faces: List[BrepFace] = None
    Trims: List[BrepTrim] = None
    """


def to_speckle_curve(occ_curve):
    """
    """

    
    
def main():
    to_rhino_curve("test")
    to_rhino_brep("test")

if __name__=="__main__":
    print("EMA timber conversion functions")
    main()