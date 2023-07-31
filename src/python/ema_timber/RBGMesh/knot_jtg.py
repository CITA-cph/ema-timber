import json
import time
import os
import gmsh

def main():
    
    date =  time.strftime("%y%m%d")
    file_dir = os.path.abspath("./example/data/"+ date)
    file_name = "knots.json"

    with open(os.path.join(file_dir, file_name), "r") as json_f:
        k_data= json.load(json_f)

    gmsh.initialize()
    gmsh.model.add(f"{date}_{file_name}")
    #gmsh.option.setNumber("Mesh.MeshSizeFromCurvature", 12)
    #gmsh.option.setNumber("Mesh.MeshSizeMin",6)
    gmsh.option.set_number("Mesh.CharacteristicLengthFromCurvature",1)
    gmsh.option.set_number("Mesh.MinimumElementsPerTwoPi",12)
    lc = 56
    knots = makeKnot(k_data)
    for k in knots:
        print (k)
        gmsh.model.addPhysicalGroup(3, [x[1] for x in k])
    gmsh.model.occ.synchronize()
    gmsh.model.mesh.generate(3)
    save_dir = os.path.join(file_dir, f"{file_name[:-5]}.msh")
    gmsh.write(save_dir)

def makeKnot(data):
    k_ls = []
    for _k in data.keys():
        subk_ls = []
        k = data[_k]
        
        for _c in k.keys():
            c = k[_c]
            parts = []
            for _p in c.keys():
                p = c[_p]
                vx,vy,vz = p["vec"]
                x,y,z = p["vtx"]
                r1, r2 = p["rad"]
                k1 = gmsh.model.occ.add_cone(x,y,z,vx,vy,vz,r1,r2)
                parts.append(k1)

            if len(parts) > 1:
                gmsh.model.occ.synchronize()
                cu, cuu = gmsh.model.occ.fuse([(3,parts[0])] , [(3,parts[1])], removeObject= True, removeTool= True)
                subk_ls.append(cu[0][1])
            else:
                subk_ls.append(parts[0])
        gmsh.model.occ.synchronize()
        cs, css = gmsh.model.occ.fragment([(3,subk_ls[-1])] , [(3, _t) for _t in subk_ls[:-1]], removeObject= True, removeTool= True)
        k_ls.append(cs)
        gmsh.model.occ.synchronize()
    return(k_ls)

if __name__ == "__main__":
    main()