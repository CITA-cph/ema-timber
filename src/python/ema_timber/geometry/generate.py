import gmsh
import classes
from classes import Knot, KnotRegion

import json, os

def json_to_knots(path:str):
    """
    Converts json file to classes.Knot
    """
    with open(path, "r") as f:
        k_data= json.load(f)

    knot_ls = []
    for k in k_data:

        id = k["id"]
        start = (k["POS"]["X"],k["POS"]["Y"],k["POS"]["Z"])
        direction = (k["Vector"]["X"],k["Vector"]["Y"],k["Vector"]["Z"])
        length = k["Knot"]["A"]
        radius = k["Knot"]["B"]
        direction = (direction[0] * length, direction[1] * length, direction[2] * length)

        if abs(k["FDR"]["E"] - length) > 1e-10:
            '''
            Create complex knot region (cone + truncated cone)
            '''
            fdr = KnotRegion(t=k["FDR"]["A"] / length, radius1=k["FDR"]["B"], radius2=k["FDR"]["D"])
            tr = KnotRegion(t=k["TR"]["A"] / length, radius1=k["TR"]["B"], radius2=k["TR"]["D"])
        else:
            '''
            Create simple knot region (cone)
            '''
            fdr = KnotRegion(t=1.0, radius1=k["FDR"]["F"], radius2=0)
            tr = KnotRegion(t=1.0, radius1=k["TR"]["F"], radius2=0)

        knot_ls.append(Knot(id=id, start=start, direction=direction, radius=radius, fdr=fdr, tr=tr))
    print (f"loaded {len(knot_ls)} knots")
    return knot_ls

def knots_to_gmsh(knot_ls, save_path:str):
    """
    generates knots in gmsh 
    """
    gmsh.initialize()
    gmsh.model.add(f"Knots")
    gmsh.option.set_number("Mesh.MeshSizeFromCurvature",12)
    gmsh.option.set_number("Mesh.MeshSizeMax",64)
    knot_meshes = []

    for k in knot_ls:
        print (f"generating - knot{k.id}")
        kr = gmsh.model.occ.add_cone(k.start[0], k.start[1], k.start[2], k.direction[0], k.direction[1], k.direction[2], 1, k.radius)

        if k.fdr.t == 1.0 :
            fdr= gmsh.model.occ.add_cone(k.start[0], k.start[1], k.start[2], k.direction[0], k.direction[1], k.direction[2], 2, k.fdr.radius1) 
        else:
            l1 = (k.direction[0]*k.fdr.t, k.direction[1]*k.fdr.t, k.direction[2]*k.fdr.t )
            l2 = (k.direction[0]*(1-k.fdr.t), k.direction[1]*(1-k.fdr.t), k.direction[2]*(1-k.fdr.t))
            fdr1 = gmsh.model.occ.add_cone(k.start[0], k.start[1], k.start[2], l1[0], l1[1], l1[2], 2, k.fdr.radius1)
            fdr2 = gmsh.model.occ.add_cone(k.start[0]+l1[0], k.start[1]+l1[1], k.start[2]+l1[2], l2[0], l2[1], l2[2], k.fdr.radius1, k.fdr.radius2)
            cu, cuu = gmsh.model.occ.fuse([(3,fdr1)] , [(3,fdr2)], removeObject= True, removeTool= True)
            fdr = cu[0][1]

        if k.tr.t == 1.0 :
            tr= gmsh.model.occ.add_cone(k.start[0], k.start[1], k.start[2], k.direction[0], k.direction[1], k.direction[2], 3, k.tr.radius1) 
        else:
            l1 = (k.direction[0]*k.tr.t, k.direction[1]*k.tr.t, k.direction[2]*k.tr.t )
            l2 = (k.direction[0]*(1-k.tr.t), k.direction[1]*(1-k.tr.t), k.direction[2]*(1-k.tr.t))
            tr1 = gmsh.model.occ.add_cone(k.start[0], k.start[1], k.start[2], l1[0], l1[1], l1[2], 3, k.tr.radius1)
            tr2 = gmsh.model.occ.add_cone(k.start[0]+l1[0], k.start[1]+l1[1], k.start[2]+l1[2], l2[0], l2[1], l2[2], k.tr.radius1, k.tr.radius2)
            cu, cuu = gmsh.model.occ.fuse([(3,tr1)] , [(3,tr2)], removeObject= True, removeTool= True)
            tr = cu[0][1]

        
        cs, css = gmsh.model.occ.fragment([(3,tr)] , [(3,kr), (3,fdr)], removeObject= True, removeTool= True)
        gmsh.model.occ.synchronize()
        p = gmsh.model.addPhysicalGroup(3, [x[1] for x in cs])
        gmsh.model.setPhysicalName(3,p, f"{k.id}")
        knot_meshes.append(cs)
    
    gmsh.model.occ.synchronize()
    gmsh.model.mesh.generate(3)
    gmsh.write(save_path)
    gmsh.finalize()
    return (knot_meshes)




if __name__ == "__main__":
    import os
    os.chdir("../../../../examples/data")
    print(os.getcwd())

    knot_path = "Log04_knots.json"
    save_path = os.path.abspath("log4_knots.msh")

    knots = json_to_knots(knot_path)
    """
    Logic to sort knots... pick which ones to mesh, etc.
    ...

    """
    knots_to_gmsh(knots[:-1], save_path )
