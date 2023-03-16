import gmsh
import classes

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
        radius = (
                    k["Knot"]["B"],
                    k["FDR"]["B"],
                    k["FDR"]["D"],
                    k["FDR"]["F"],
                    k["TR"]["B"],
                    k["TR"]["D"],
                    k["TR"]["F"],
                )
        height = (
                    k["Knot"]["A"],
                    k["FDR"]["A"],
                    k["FDR"]["C"],
                    k["FDR"]["E"],
                    k["TR"]["A"],
                    k["TR"]["C"],
                    k["TR"]["E"],
                )
        KR = (height[0],radius[0])
        FDR = (height[1],radius[1],height[2],radius[2],height[3],radius[3])
        TR = (height[4],radius[4],height[5],radius[5],height[6],radius[6])
        knot_obj = classes.Knot(id=id , start=start, direction=direction, radius=radius, height=height , KR=KR, FDR=FDR, TR=TR )
        knot_ls.append(knot_obj)

    return knot_ls

def knots_to_gmsh(knot_path:str, save_path:str):
    """
    generates knots in gmsh 
    """

    knot_ls  = json_to_knots(knot_path)

    gmsh.initialize()
    gmsh.model.add(f"Knots")
    gmsh.option.set_number("Mesh.CharacteristicLengthFromCurvature",1)
    gmsh.option.set_number("Mesh.MinimumElementsPerTwoPi",12)
    knot_meshes = []

    for k in knot_ls[:34]:
        kr = gmsh.model.occ.add_cone(k.start[0], k.start[1], k.start[2], k.direction[0]*k.KR[0], k.direction[1]*k.KR[0], k.direction[2]*k.KR[0], 1, k.KR[1])

        if k.FDR[4] != 0 and k.FDR[5] != 0 :
            fdr= gmsh.model.occ.add_cone(k.start[0], k.start[1], k.start[2], k.direction[0]*k.FDR[4], k.direction[1]*k.FDR[4], k.direction[2]*k.FDR[4], 2, k.FDR[5]) 
        else:
            fdr1 = gmsh.model.occ.add_cone(k.start[0], k.start[1], k.start[2], k.direction[0]*k.FDR[0], k.direction[1]*k.FDR[0], k.direction[2]*k.FDR[0], 2, k.FDR[1])
            fdr2 = gmsh.model.occ.add_cone(k.start[0] + (k.direction[0]*k.FDR[0]), k.start[1]+ (k.direction[1]*k.FDR[0]), k.start[2]+ (k.direction[2]*k.FDR[0]), k.direction[0]*k.FDR[2], k.direction[1]*k.FDR[2], k.direction[2]*k.FDR[2], k.FDR[1], k.FDR[3])
            #gmsh.model.occ.synchronize()
            cu, cuu = gmsh.model.occ.fuse([(3,fdr1)] , [(3,fdr2)], removeObject= True, removeTool= True)
            fdr = cu[0][1]

        if k.TR[4] != 0 and k.TR[5] != 0:
            tr= gmsh.model.occ.add_cone(k.start[0], k.start[1], k.start[2], k.direction[0]*k.TR[4], k.direction[1]*k.TR[4], k.direction[2]*k.TR[4], 3, k.TR[5]) 
        else:
            tr1 = gmsh.model.occ.add_cone(k.start[0], k.start[1], k.start[2], k.direction[0]*k.TR[0], k.direction[1]*k.TR[0], k.direction[2]*k.TR[0], 3, k.TR[1])
            tr2 = gmsh.model.occ.add_cone(k.start[0] + (k.direction[0]*k.TR[0]), k.start[1]+ (k.direction[1]*k.TR[0]), k.start[2]+ (k.direction[2]*k.TR[0]), k.direction[0]*k.TR[2], k.direction[1]*k.TR[2], k.direction[2]*k.TR[2], k.TR[1], k.TR[3])
            #gmsh.model.occ.synchronize()
            cu, cuu = gmsh.model.occ.fuse([(3,tr1)] , [(3,tr2)], removeObject= True, removeTool= True)
            tr = cu[0][1] 

        
        cs, css = gmsh.model.occ.fragment([(3,tr)] , [(3,fdr), (3,kr) ], removeObject= True, removeTool= True)
        gmsh.model.occ.synchronize()
        p = gmsh.model.addPhysicalGroup(3, [x[1] for x in cs])
        gmsh.model.setPhysicalName(3,p, f"{k.id}")
        knot_meshes.append(cs)
    
    gmsh.model.occ.synchronize()
    gmsh.model.mesh.generate(3)
    gmsh.write(save_path)
    gmsh.finalize()
    




if __name__ == "__main__":

    knot_path = "C:/Users/ceep/Det Kongelige Akademi/ERC_TIMBER_TRACK - General/02_PROJECTS/23_06 - EMA Shed/data/jsonfiles/Log04_knots.json"
    save_path = os.path.abspath("./data/log4_knots.msh")
    knots_to_gmsh(knot_path, save_path )