import logging

import rhino3dm
import specklepy
from specklepy.objects.geometry import Brep, Curve, Point

from rhino3dm import Point3d, Point4d, Vector3d
import OCC
from OCC.Core import gp
from OCC.Core.Geom import Geom_BSplineCurve
from OCC.Core.gp import gp_Pnt, gp_Dir, gp_Ax2
from OCC.Core.TColStd import TColStd_Array1OfReal, TColStd_Array1OfInteger
from OCC.Core.TColgp import TColgp_Array1OfPnt

def to_rhino_curve(occ_curve : Geom_BSplineCurve):

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

def to_rhino_brep(occ_brep : rhino3dm.Brep):

    rbrep = rhino3dm.Brep()
    #print(dir(rbrep))

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

def to_occ_brep(speckle_brep):

    return

def to_speckle_curve(occ_curve : Geom_BSplineCurve):
    """
    OCC BSplineCurve to Speckle Curve
    """

    degree = occ_curve.Degree()
    closed = occ_curve.IsClosed()

    poles = occ_curve.Poles()
    points = []
    weights = []

    for i in range(poles.Length()):
        pt = poles.Value(i + 1)
        points.append(Point(x=pt.X(), y=pt.Y(), z=pt.Z()))
        weights.append(occ_curve.Weight(i))

    knots = []
    for i in range(occ_curve.Knots().Length()):
        knots.append(occ_curve.Knots().Value(i + 1))

    mults = []
    for i in range(occ_curve.Multiplicities().Length()):
        mults.append(occ_curve.Multiplicities().Value(i + 1))

    knots_flat = []
    for i, m in enumerate(mults):
        for j in range(m):
            knots_flat.append(knots[i])

    return Curve(degree=degree, points=points, weights=weights, knots=knots_flat, closed=closed)

def speckle_knots_to_occ(speckle_curve):
    """
    Speckle Curve knots to occ knots
    """
    last_knot = speckle_curve.knots[0]
    current_knot = speckle_curve.knots[1]

    knots = []
    mults = []
    mult = 1

    epsilon = OCC.Core.gp.gp.Resolution()
    for i in range(1, len(speckle_curve.knots)):

        current_knot = speckle_curve.knots[i]
        if abs(last_knot - current_knot) > epsilon:

            knots.append(last_knot)
            mults.append(mult)

            mult = 1
            last_knot = current_knot
        else:
            mult += 1

    knots.append(current_knot)
    mults.append(mult)

    mults[0] = speckle_curve.degree + 1
    mults[-1] = speckle_curve.degree + 1

    return knots, mults

def to_occ_curve(speckle_curve : Curve):
    """
    Speckle Curve to OCC Geom_BSplineCurve
    """

    degree = speckle_curve.degree
    closed = bool(speckle_curve.closed)

    poles = TColgp_Array1OfPnt(1, len(speckle_curve.points))
    for i, point in enumerate(speckle_curve.points, 1):
        poles.SetValue(i, gp_Pnt(point.x, point.y, point.z))

    weights = TColStd_Array1OfReal(1, len(speckle_curve.weights))

    for i, weight in enumerate(speckle_curve.weights, 1):
        weights.SetValue(i, weight)

    knots_data, mults_data = speckle_knots_to_occ(speckle_curve)

    knots = TColStd_Array1OfReal(1, len(knots_data))
    for i, knot in enumerate(knots_data, 1):
        knots.SetValue(i, knot)

    mults = TColStd_Array1OfInteger(1, len(mults_data))
    for i, mult in enumerate(mults_data, 1):
        mults.SetValue(i, mult)

    return Geom_BSplineCurve(poles, weights, knots, mults, degree, closed)

def make_dummy_speckle_curve():
    degree = 3
    points = [
        Point(x=0,y=0,z=0),
        Point(x=500,y=0,z=0),
        Point(x=1000,y=500,z=0),
        Point(x=1500,y=500,z=0),
        Point(x=2000,y=250,z=0),
    ]
    weights = [1.0, 1.0, 1.0, 1.0, 1.0]
    knots = [1.0, 1.0, 1.0, 1.0, 3.0, 5.0, 5.0, 5.0, 5.0]

    return Curve(degree=degree, points=points, weights=weights, knots=knots, closed=False)

def test_speckle_curve_to_occ():

    crv = make_dummy_speckle_curve()
    occ_crv = to_occ_curve(crv)
    print(occ_crv)

    return occ_crv

def test_occ_curve_to_speckle():

    crv = make_dummy_speckle_curve()
    occ_crv = to_occ_curve(crv)

    speckle_curve = to_speckle_curve(occ_crv)

    print(crv.__dict__)
    print()
    print(speckle_curve.__dict__)
    print()

    s0 = str(crv.__dict__)
    s1 = str(speckle_curve.__dict__)
    print(f"equivalent: {s0 == s1}")

    return speckle_curve


def main():
    to_rhino_curve("test")
    to_rhino_brep("test")

    test_occ_curve_to_speckle()



if __name__=="__main__":
    print("EMA timber conversion functions")
    #print(dir(OCC))
    main()