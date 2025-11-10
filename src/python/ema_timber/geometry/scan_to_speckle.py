import os
import logging
import datetime
import gmsh
import specklepy
from specklepy.objects import Base
from specklepy.objects.geometry import Point, Vector, Plane, Circle, Mesh
from specklepy.objects.other import Material
from specklepy.serialization.base_object_serializer import BaseObjectSerializer

def scan_to_speckle(json_path, scale=1.0):
    import json

    dirname = os.path.dirname(json_path)

    with open(data_path, 'r') as file:
        data = json.load(file)

    if not data:
        raise Exception("Failed to load scan data.")

    # Check and retrieve data
    if "name" not in data.keys():
        name = "A00"
    else:
        name = data["name"]

    outline = data["outline"]
    knots = data["knots"]
    thickness = data["thickness"]
    img_path = ""

    if "image" in data.keys():
        img_path = os.path.abspath(os.path.join(dirname, data["image"]["path"]))
        img_width = data["image"]["width"] * scale
        img_height = data["image"]["height"] * scale

    # Begin geometry processing
    gmsh.initialize()

    # Process knots
    knot_objects = []
    knot_circles = []

    for bb in knots:
        w = bb[1][0] - bb[0][0]
        h = bb[1][1] - bb[0][1]

        rad = min(w, h)

        c = ((bb[0][0] + bb[1][0]) * 0.5, (bb[0][1] + bb[1][1]) * 0.5)
        kc = Point(x=c[0] * scale,y=c[0] * scale,z=0)
        kr = rad * scale
        sk = Base(
            centre=kc, 
            radius=kr, 
            representations=[Circle(
                plane=Plane(
                    origin=kc, 
                    normal=Vector(x=0, y=0, z=1)),
                radius=kr)])

        knot_objects.append(sk)

        gmsh.model.occ.addPoint(c[0] * scale, c[1] * scale, 0)

        circ = gmsh.model.occ.addCircle(c[0] * scale, c[1] * scale, 0, rad * scale)
        knot_circles.append(circ)

    # Process board outline
    pts = []
    for pt in outline:
        pts.append(gmsh.model.occ.addPoint(pt[0] * scale, pt[1] * scale, 0))

    pts.append(pts[0]) # Close polyline

    # Create individual edges
    lines = []
    for i in range(len(pts)-1):
        lines.append(gmsh.model.occ.addLine(pts[i], pts[i+1]))

    # Create closed wire from edges
    wire = gmsh.model.occ.addWire(lines)

    # Create planar surface from wire
    srf = gmsh.model.occ.addPlaneSurface([wire])
    
    # gmsh.model.occ.synchronize()
    # gmsh.model.mesh.embed(1, knot_circles, 2, srf)

    # Create extrusion
    ext = gmsh.model.occ.extrude([(2, srf)], 0, 0, -thickness)

    gmsh.model.occ.synchronize()

    # Generate 2D mesh
    gmsh.model.mesh.generate(2)

    # Get all nodes
    nodeTags, coord, _ = gmsh.model.mesh.getNodes(dim=2, tag=-1, includeBoundary=True, returnParametricCoord = False)

    # Create node map
    nodeMap = {}
    for i in range(len(nodeTags)):
        nodeMap[nodeTags[i]] = i

    verts = []
    tex = []
    faces = []

    # Populate vertex list
    for i in range(len(nodeTags)):
        verts.append((coord[i * 3], coord[i * 3 + 1], coord[i * 3 + 2]))

    # Populate texture coordinates list
    for v in verts:
        tex.append((v[0] / img_width, 1.0 - v[1] / img_height))

    logging.debug(f"Num. nodes: {len(nodeTags)}")

    # Get all 2D elements (faces)
    elementTypes, elementTags, elementNodeTags = gmsh.model.mesh.getElements(dim=2, tag=-1)

    # Don't need Gmsh anymore, so finalize it
    #gmsh.write(filepath)
    gmsh.finalize()

    # Populate faces list
    for i in range(len(elementTypes)):
        if elementTypes[i] == 2:
            for j in range(0, len(elementNodeTags[i]), 3):
                a = nodeMap[elementNodeTags[i][j]] + 1
                b = nodeMap[elementNodeTags[i][j + 1]]+ 1
                c = nodeMap[elementNodeTags[i][j + 2]]+ 1

                faces.append((a, b, c))

    tex_name = os.path.relpath(os.path.join(dirname, name + ".png"), dirname)

    # Create Speckle material
    material = Material(
        name=name,
        diffuse_texture=tex_name)

    # Create Speckle mesh
    mesh = Mesh(
        vertices=[], 
        faces=[], 
        textureCoordinates=[], 
        material=material, 
        units="m")

    for v in verts:
        mesh.vertices.extend(v)
    for f in faces:
        mesh.faces.append(1)
        mesh.faces.extend(f)
    for t in tex:
        mesh.textureCoordinates.extend(t)

    # Create Speckle base object for assembling everything
    speckle_object = Base(
        units="m", 
        name=name, 
        applicationId="EmaScan", 
        authors=["Ee Pin Choo", "Tom Svilans"], 
        date=datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        raw_scan_data=data,
        mesh=mesh,
        knots=knot_objects)

    # Serialize and write to file
    serializer = BaseObjectSerializer()
    obj_id, obj = serializer.traverse_base(speckle_object)

    with open(os.path.join(dirname, name + ".json"), "w") as file:
        file.write(json.dumps(obj, indent=2, sort_keys=True))

if __name__=="__main__":
    logging.basicConfig(level=logging.DEBUG, format='%(levelname)s: %(message)s')

    import tkinter as tk
    from tkinter import filedialog

    data_path = filedialog.askopenfilename()
    if os.path.exists(data_path):

        # Hard-coded data path
        # data_path = r"C:\Users\tsvi\Det Kongelige Akademi\ERC_TIMBER_TRACK - General\02_PROJECTS\23_11 - LinearScanner\Software\230129_build_0.1\linearscanner\data\240208_dirtyHalf\B\data.json"

        # Hard-coded scale values from earlier experiment
        scale = 1.6666666666666667 * 0.173641323247737 * 0.001

        # Scale should be taken from the JSON data file
        scan_to_speckle(data_path, scale=scale)