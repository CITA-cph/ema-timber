import json
import gmsh
import os
import logging

def export_obj(directory, name, verts, faces, tex_coords=None, material_name=None, texture_name=None):

    if not material_name:
        material_name = "ScanMaterial"

    if not name:
        name = "ScanObject"

    obj_path = os.path.join(directory, name + ".obj")
    mtl_path = os.path.join(directory, name + ".mtl")

    # Write OBJ
    with open(obj_path, 'w') as file:

        if mtl_path:
            file.write(f"mtllib {name + '.mtl'}\n")

        file.write("\n");
        for v in verts:
            file.write(f"v {v[0]} {v[1]} {v[2]}\n")

        if tex_coords:
            file.write("\n");
            for t in tex_coords:
                file.write(f"vt {t[0]} {t[1]}\n")

        file.write("\n");
        file.write(f"g {name}\n")
        file.write(f"usemtl {material_name}\n")

        file.write("\n");
        if tex_coords:
            for f in faces:
                file.write(f"f {f[0]}/{f[0]} {f[1]}/{f[1]} {f[2]}/{f[2]}\n")
        else:
            for f in faces:
                file.write(f"f {f[0]} {f[1]} {f[2]}\n")

    if mtl_path:
        # Write MTL
        with open(mtl_path, 'w') as file:
            file.write(f"newmtl {material_name}\n")
            file.write("Kd 0.5 0.5 0.5\n")

            if texture_name:
                file.write(f"map_Kd {texture_name}\n")

def scan_to_obj(json_path, scale=1.0):
    import json

    dirname = os.path.dirname(json_path)

    with open(data_path, 'r') as file:
        data = json.load(file)

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
    knot_circles = []

    for bb in knots:
        w = bb[1][0] - bb[0][0]
        h = bb[1][1] - bb[0][1]

        rad = min(w, h)

        c = ((bb[0][0] + bb[1][0]) * 0.5, (bb[0][1] + bb[1][1]) * 0.5)

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

    # Create file paths
    obj_path = os.path.join(dirname, name + ".obj")
    mtl_path = os.path.join(dirname, name + ".mtl")
    #tex_name = os.path.abspath(img_path)
    tex_name = os.path.join(dirname, name + ".png")

    # Copy the image and rename it
    with open(img_path, 'rb') as image_file:
        size = image_file.seek(0, 2)
        image_file.seek(0,0)
        logging.debug(f"img size {size}")

        with open(tex_name, 'wb') as texture_file:
            num_bytes = texture_file.write(image_file.read(size))
            logging.debug(f"wrote {num_bytes} to file")

    logging.debug(f"obj_path {obj_path}")
    logging.debug(f"mtl_path {mtl_path}")
    logging.debug(f"tex_name {tex_name}")
    logging.debug(f"image exists: {os.path.exists(tex_name)}")
    logging.debug(f"tex_name (rel) {os.path.relpath(tex_name, dirname)}")

    export_obj(dirname, name, verts, faces, tex, name, os.path.relpath(tex_name, dirname))


if __name__=="__main__":
    logging.basicConfig(level=logging.DEBUG, format='%(levelname)s: %(message)s')

    import tkinter as tk
    from tkinter import filedialog

    data_path = filedialog.askopenfilename()

    # Hard-coded data path
    # data_path = r"C:\Users\tsvi\Det Kongelige Akademi\ERC_TIMBER_TRACK - General\02_PROJECTS\23_11 - LinearScanner\Software\230129_build_0.1\linearscanner\data\240208_dirtyHalf\B\data.json"

    # Hard-coded scale values from earlier experiment
    scale = 1.6666666666666667 * 0.173641323247737 * 0.001

    # Scale should be taken from the JSON data file
    scan_to_obj(data_path, scale=scale)