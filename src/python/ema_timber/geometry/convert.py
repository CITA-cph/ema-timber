import logging

import rhino3dm
import specklepy
from specklepy.objects.geometry import Brep, Curve, Point, Line, Circle, Plane, Vector

from rhino3dm import Point3d, Point4d, Vector3d
import OCC
from OCC.Core import gp
from OCC.Core.BRepBuilderAPI import BRepBuilderAPI_MakeFace, BRepBuilderAPI_MakeWire, BRepBuilderAPI_MakeEdge
from OCC.Core.BRepBuilderAPI import BRepBuilderAPI_MakeVertex, BRepBuilderAPI_MakeShell
from OCC.Core.BRep import BRep_Tool, BRep_Builder
from OCC.Core.GC import GC_MakeSegment, GC_MakeCircle
from OCC.Core.GCE2d import GCE2d_MakeSegment, GCE2d_MakeCircle 
from OCC.Core.TopoDS import TopoDS_Builder, TopoDS_Compound


from OCC.Core.Geom import Geom_BSplineCurve, Geom_Line, Geom_TrimmedCurve, Geom_BSplineSurface
from OCC.Core.Geom2d import Geom2d_Curve, Geom2d_BSplineCurve
from OCC.Core.gp import gp_Pnt, gp_Dir, gp_Ax2, gp_Vec
from OCC.Core.gp import gp_Pnt2d, gp_Dir2d, gp_Ax22d
from OCC.Core.TColStd import TColStd_Array1OfReal, TColStd_Array2OfReal, TColStd_Array1OfInteger
from OCC.Core.TColgp import TColgp_Array1OfPnt, TColgp_Array2OfPnt
from OCC.Core.TColgp import TColgp_Array1OfPnt2d

class RhinoToOcc:
    def gp_Pnt_to_string(pt):
        return f"{pt.X():.3f} {pt.Y():.3f} {pt.Z():.3f}"

    def gp_Pnt2d_to_string(pt):
        return f"{pt.X():.3f} {pt.Y():.3f}"

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


class SpeckleToOcc:

    @staticmethod
    def speckle_knots_to_occ(knots, degree):
        """
        Speckle Curve knots to occ knots
        """
        last_knot = knots[0]
        current_knot = knots[1]

        nknots = []
        mults = []
        mult = 1

        epsilon = OCC.Core.gp.gp.Resolution()
        for i in range(1, len(knots)):

            current_knot = knots[i]
            if abs(last_knot - current_knot) > epsilon:

                nknots.append(last_knot)
                mults.append(mult)

                mult = 1
                last_knot = current_knot
            else:
                mult += 1

        nknots.append(current_knot)
        mults.append(mult)

        mults[0] = degree + 1
        mults[-1] = degree + 1

        return nknots, mults

    @staticmethod
    def point(speckle_point: Point):
        return gp_Pnt(speckle_point.x, speckle_point.y, speckle_point.z)

    @staticmethod
    def point2d(speckle_point: Point):
        return gp_Pnt2d(speckle_point.x, speckle_point.y)

    @staticmethod
    def direction(speckle_vector: Vector):
        return gp_Dir(speckle_vector.x, speckle_vector.y, speckle_vector.z)

    @staticmethod
    def direction2d(speckle_vector: Vector):
        return gp_Dir2d(speckle_vector.x, speckle_vector.y)

    @staticmethod
    def plane(speckle_plane: Plane):
        return gp_Ax2(SpeckleToOcc.point(speckle_plane.origin), 
            SpeckleToOcc.direction(speckle_plane.normal),
            SpeckleToOcc.direction(speckle_plane.xdir))

    @staticmethod
    def plane2d(speckle_plane: Plane):
        return gp_Ax22d(SpeckleToOcc.point2d(speckle_plane.origin), 
            SpeckleToOcc.direction2d(speckle_plane.xdir))

    @staticmethod
    def circle(speckle_circle: Circle):
        return GC_MakeCircle(SpeckleToOcc.plane(speckle_circle.plane), speckle_circle.radius).Value()
        
    @staticmethod
    def circle2d(speckle_circle: Circle):
        return GCE2d_MakeCircle(SpeckleToOcc.plane2d(speckle_circle.plane), speckle_circle.radius).Value()

    @staticmethod
    def line(speckle_line: Line):
        start = gp_Pnt(speckle_line.start.x, speckle_line.start.y, speckle_line.start.z)
        end = gp_Pnt(speckle_line.end.x, speckle_line.end.y, speckle_line.end.z)

        return GC_MakeSegment(start, end).Value()

    @staticmethod
    def line2d(speckle_line: Line):
        start = gp_Pnt2d(speckle_line.start.x, speckle_line.start.y)
        end = gp_Pnt2d(speckle_line.end.x, speckle_line.end.y)

        # print(f"  {gp_Pnt2d_to_string(start)}")
        # print(f"  {speckle_line.start}")
        # print(f"  {gp_Pnt2d_to_string(end)}")
        # print(f"  {speckle_line.end}")

        return GCE2d_MakeSegment(start, end).Value()

    @staticmethod
    def curve(speckle_curve : Curve):
        """
        Speckle Curve to OCC Geom_BSplineCurve
        """
        # print(f"Converting curve type {speckle_curve.speckle_type}")

        if speckle_curve.speckle_type.endswith("Line"):
            return SpeckleToOcc.line(speckle_curve)
        elif speckle_curve.speckle_type.endswith("Circle"):
            return SpeckleToOcc.circle(speckle_curve)
        else:
            degree = speckle_curve.degree
            closed = bool(speckle_curve.closed)

        N = len(speckle_curve.points) // 3
        #print(f"num values {len(speckle_curve.points)}, num pts {N}")
        #print(f"num weights {len(speckle_curve.weights)}")
        #print(f"degree {degree}")
        #print(f"periodic {closed}")
        poles = TColgp_Array1OfPnt(1, N)

        for i in range(N):
            poles.SetValue(i + 1, gp_Pnt(
                speckle_curve.points[i * 3 + 0], 
                speckle_curve.points[i * 3 + 1], 
                speckle_curve.points[i * 3 + 2]))

        weights = TColStd_Array1OfReal(1, len(speckle_curve.weights))

        for i, weight in enumerate(speckle_curve.weights, 1):
            weights.SetValue(i, weight)

        knots_data, mults_data = SpeckleToOcc.speckle_knots_to_occ(speckle_curve.knots, speckle_curve.degree)

        knots = TColStd_Array1OfReal(1, len(knots_data))
        for i, knot in enumerate(knots_data, 1):
            knots.SetValue(i, knot)

        mults = TColStd_Array1OfInteger(1, len(mults_data))
        for i, mult in enumerate(mults_data, 1):
            mults.SetValue(i, mult)

        return Geom_BSplineCurve(poles, weights, knots, mults, degree, closed)

    @staticmethod
    def curve2d(speckle_curve : Curve):
        """
        Speckle Curve to OCC Geom_BSplineCurve
        """
        # print(f"Converting 2d curve type {speckle_curve.speckle_type}")

        if speckle_curve.speckle_type.endswith("Line"):
            return SpeckleToOcc.line2d(speckle_curve)
        elif speckle_curve.speckle_type.endswith("Circle"):
            return SpeckleToOcc.circle2d(speckle_curve)
        else:
            degree = speckle_curve.degree
            closed = bool(speckle_curve.closed)


        N = len(speckle_curve.points) // 3
        #print(f"num values {len(speckle_curve.points)}, num pts {N}")
        #print(f"num weights {len(speckle_curve.weights)}")
        #print(f"degree {degree}")
        #print(f"periodic {closed}")

        poles = TColgp_Array1OfPnt2d(1, N)

        for i in range(N):
            poles.SetValue(i + 1, gp_Pnt2d(
                speckle_curve.points[i * 3 + 0], 
                speckle_curve.points[i * 3 + 1]))

        weights = TColStd_Array1OfReal(1, len(speckle_curve.weights))

        for i, weight in enumerate(speckle_curve.weights, 1):
            weights.SetValue(i, weight)

        knots_data, mults_data = SpeckleToOcc.speckle_knots_to_occ(speckle_curve.knots, speckle_curve.degree)

        knots = TColStd_Array1OfReal(1, len(knots_data))
        for i, knot in enumerate(knots_data, 1):
            knots.SetValue(i, knot)

        mults = TColStd_Array1OfInteger(1, len(mults_data))
        for i, mult in enumerate(mults_data, 1):
            mults.SetValue(i, mult)

        crv = Geom2d_BSplineCurve(poles, weights, knots, mults, degree, closed)
        # print(f"Created {crv}")
        return crv

    @staticmethod
    def surface(srf):
        poles = TColgp_Array2OfPnt(1, srf.countU, 1, srf.countV)
        degreeU = srf.degreeU
        degreeV = srf.degreeV
        periodicU = srf.closedU
        periodicV = srf.closedV

        knotsU_data, multsU_data = SpeckleToOcc.speckle_knots_to_occ(srf.knotsU, srf.degreeU)
        knotsV_data, multsV_data = SpeckleToOcc.speckle_knots_to_occ(srf.knotsV, srf.degreeV)

        knotsU = TColStd_Array1OfReal(1, len(knotsU_data))
        for i, knot in enumerate(knotsU_data):
            knotsU.SetValue(i + 1, knot)

        multsU = TColStd_Array1OfInteger(1, len(multsU_data))
        for i, mult in enumerate(multsU_data):
            multsU.SetValue(i + 1, mult)

        knotsV = TColStd_Array1OfReal(1, len(knotsV_data))
        for i, knot in enumerate(knotsV_data):
            knotsV.SetValue(i + 1, knot)

        multsV = TColStd_Array1OfInteger(1, len(multsV_data))
        for i, mult in enumerate(multsV_data):
            multsV.SetValue(i + 1, mult)

        i = 0
        #print(f"processing surface {srf}")
        #for pdat in srf.pointData:
        #    print(f"    {pdat:.3f}")

        weights = TColStd_Array2OfReal(1, srf.countU, 1, srf.countV)
        for u in range(srf.countU):
            for v in range(srf.countV):
                pt = gp_Pnt(srf.pointData[i], srf.pointData[i + 1], srf.pointData[i + 2])
                poles.SetValue(u + 1, v + 1, pt)
                weights.SetValue(u + 1, v + 1, srf.pointData[i + 3])

                #print(f"{u+1}, {v+1} : {gp_Pnt_to_string(poles.Value(u + 1, v + 1))}")
                i += 4

        return Geom_BSplineSurface(poles, weights, knotsU, knotsV, multsU, multsV, degreeU, degreeV, periodicU, periodicV)

    @staticmethod
    def brep(speckle_brep):

        print(f"Speckle Brep stats")
        print(f"   num vertices {len(speckle_brep.Vertices)}")
        print(f"   num curve3d  {len(speckle_brep.Curve3D)}")
        print(f"   num curve2d  {len(speckle_brep.Curve2D)}")
        print(f"   num surfaces {len(speckle_brep.Surfaces)}")
        print(f"   num edges    {len(speckle_brep.Edges)}")
        print(f"   num faces    {len(speckle_brep.Faces)}")

        vertices = [BRepBuilderAPI_MakeVertex(gp_Pnt(p.x, p.y, p.z)).Vertex() for p in speckle_brep.Vertices]

        #for vert in vertices:
        #    print(f"vert   {gp_Pnt_to_string(BRep_Tool.Pnt(vert))}")

        curve3d = [SpeckleToOcc.curve(crv) for crv in speckle_brep.Curve3D]
        print(f"num 3d curves: {len(curve3d)}")
        curve2d = [SpeckleToOcc.curve2d(crv) for crv in speckle_brep.Curve2D]
        print(f"num 2d curves: {len(curve2d)}")
        surfaces = [SpeckleToOcc.surface(srf) for srf in speckle_brep.Surfaces]
        print(f"num surfaces: {len(surfaces)}")

        print()
        print("Edges #################")
        print()

        edges = []
        for be in speckle_brep.Edges:
            crv3d = curve3d[be.Curve3dIndex]
            startv = vertices[be.StartIndex]
            endv = vertices[be.EndIndex]

            print("edge")
            print(f"    Curve3dIndex {be.Curve3dIndex}")
            print(f"    StartIndex {be.StartIndex}")
            print(f"    EndIndex {be.EndIndex}")

            # print(f"{crv3d}")
            # print(f"    crv first {gp_Pnt_to_string(crv3d.Value(crv3d.FirstParameter()))}")
            # print(f"    crv last {gp_Pnt_to_string(crv3d.Value(crv3d.LastParameter()))}")
            # print(f"    startv   {gp_Pnt_to_string(BRep_Tool.Pnt(startv))}")
            # print(f"    endv   {gp_Pnt_to_string(BRep_Tool.Pnt(endv))}")
                #print(f"{be.StartIndex}")
                #print(f"{be.EndIndex}")

            if be.StartIndex == be.EndIndex:
                edge = BRepBuilderAPI_MakeEdge(crv3d)
            else:
                edge = BRepBuilderAPI_MakeEdge(crv3d, startv, endv)
            edges.append(edge.Edge())

        print()
        print("Trims #################")
        print()

        trims = []
        trim_count = 0
        for t in speckle_brep.Trims:
            face = speckle_brep.Faces[t.FaceIndex]

            print(f"trim")
            print(f"    CurveIndex {t.CurveIndex}")
            print(f"    EdgeIndex {t.EdgeIndex}")
            print(f"    SurfaceIndex {face.SurfaceIndex}")

            tcurve = curve2d[t.CurveIndex]
            print(f"    {tcurve}")

            if isinstance(tcurve, Geom2d_Curve):
                edge2d = BRepBuilderAPI_MakeEdge(tcurve, surfaces[face.SurfaceIndex]).Edge()
            else:
                edge2d = BRepBuilderAPI_MakeEdge(tcurve).Edge()

            trims.append(edge2d)
            trim_count += 1
            pass

        print()
        print("Loops / Wires #########")
        print()

        wires = []

        for loop in speckle_brep.Loops:
            wire = BRepBuilderAPI_MakeWire()
            print(f"loop {loop}")
            print(f"    TrimIndices {loop.TrimIndices}")
            for ti in loop.TrimIndices:
                wire.Add(trims[ti])
                print(f"    added {ti}")

            if not wire.IsDone():
                print(f"Wire failed: {wire.Error()}")
                wires.append(None)
                continue

            wires.append(wire.Wire())

        print()
        print("Faces #################")
        print()

        faces = []
        for sface in speckle_brep.Faces:

            print(f"face")
            print(f"    outerloop {sface.OuterLoopIndex}")
            print(f"    surface   {sface.SurfaceIndex}")
            print(f"    loop ids   {sface.LoopIndices}")

            if wires[sface.OuterLoopIndex] is None:
                print(f"Couldn't find loop {sface.OuterLoopIndex}")
                continue

            face = BRepBuilderAPI_MakeFace(surfaces[sface.SurfaceIndex], wires[sface.OuterLoopIndex], False)

            for hole in sface.LoopIndices:
                if hole != sface.OuterLoopIndex:
                    face.Add(wires[hole])

            if not face.IsDone():
                print(f"face did not finish: {face.Error()}")
                continue

            faces.append(face.Face())

        print()
        print("Shells #################")
        print()

        shells = []
        for srf in surfaces:
            shell = BRepBuilderAPI_MakeShell(srf).Shell()
            print(f"Made shell: {shell}")
            shells.append(shell)

        print()
        print("Compound #################")
        print()

        c = TopoDS_Compound()
        topo = TopoDS_Builder()
        topo.MakeCompound(c)


        for face in faces:
            topo.Add(c, face)
            pass

        for shell in shells:
            if shell is not None:
                #topo.Add(c, shell)
            #break
                pass

        for i, trim in enumerate(trims):
            #print(f"    adding TRIM {i} {trim}")
            #topo.Add(c, trim)
            pass

        for wire in wires:
            if wire is not None:
                #topo.Add(c, wire)
                pass

        return c


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


def main():
    to_rhino_curve("test")
    to_rhino_brep("test")

    test_occ_curve_to_speckle()



if __name__=="__main__":
    print("EMA timber conversion functions")
    #print(dir(OCC))
    main()