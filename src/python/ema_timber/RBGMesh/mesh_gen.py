from brep_jtg import makeBrep
from knot_jtg import makeKnot
import json, os, time, gmsh
import numpy as np
def main():

    # File directories

    date =  time.strftime("%y%m%d")
    file_dir = os.path.abspath("./example/data/"+ date)

    brep_name = "model.json"
    knot_name = "knots.json"

    with open(os.path.join(file_dir, brep_name), "r") as json_f:
        b_data= json.load(json_f)

    with open(os.path.join(file_dir, knot_name), "r") as json_f:
        k_data= json.load(json_f)

    # Gmsh settings

    gmsh.initialize()
    gmsh.model.add(f"{date}_{brep_name}")

    gmsh.option.set_number("Mesh.CharacteristicLengthFromCurvature",1)
    gmsh.option.set_number("Mesh.MinimumElementsPerTwoPi",12)

    lc = 48


    # Making Breps

    volumes = makeBrep(b_data, lc)

    # Making Knots

    knots = makeKnot(k_data)
    
    # Intersections
    cw = []
    kr = []
    fdr = []
    tr = []
    ls = []
    for  v in volumes:
        o = (3,v)
        k_ls = []
        for k in knots[:5]:
            for k1 in k:
                k_ls.append(k1)
        bs, bss = gmsh.model.occ.fragment([o], k_ls, removeObject= True, removeTool=True)
        print (f"tree: {bss}")
        for i in bss[0][1:]:
            for id , j in enumerate(bss[1:]):
                if len(j) > 2:
                    j = j[1:]
                if i in j:
                    ls.append(id)
                    break
        
        new_bss = [x for _, x in sorted(zip(ls, bss[0][1:] ))]
    for i in range(0,len(new_bss),3):
        kr.append(new_bss[i])
        tr.append(new_bss[i+1])
        fdr.append(new_bss[i+2])
    cw.append(bss[0][0])
    # Save gmsh
    gmsh.model.occ.synchronize()
    
    cwr_gid= gmsh.model.addPhysicalGroup(3, [x[1] for x in cw])
    kr_gid=gmsh.model.addPhysicalGroup(3, [x[1] for x in kr])
    fdr_gid=gmsh.model.addPhysicalGroup(3, [x[1] for x in fdr])
    tr_gid=gmsh.model.addPhysicalGroup(3, [x[1] for x in tr])
    gmsh.model.mesh.generate(3)
    gmsh.model.occ.synchronize()

    save_dir = os.path.join(file_dir, f"{brep_name[:-5]}.inp")
    gmsh.write(save_dir)
    save_dir = os.path.join(file_dir, f"{brep_name[:-5]}.msh")
    gmsh.write(save_dir)
    gmsh.finalize()
    print (bss[0][1:] , len (bss[0][1:]))
    print (ls , len(ls))
    print (new_bss)
    print (cw)
    print (kr)
    print (fdr)
    print (tr)

if __name__ == "__main__":
    main()