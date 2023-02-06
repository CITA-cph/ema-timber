import json
import time
import os
import gmsh

def main():

    date =  time.strftime("%y%m%d")
    file_dir = os.path.abspath("./example/data/"+ date)
    file_name = "model.json"
    f = open(os.path.join(file_dir, file_name))
    with open(os.path.join(file_dir, file_name), "r") as json_f:
        data= json.load(json_f)

    gmsh.initialize()
    gmsh.model.add(f"{date}_{file_name}")
    gmsh.option.setNumber("Mesh.MeshSizeFromCurvature", 36)
    gmsh.option.setNumber("Mesh.MeshSizeMin",4)
    lc = 48
    volumes = []

    for _b in data.keys():
        print (f"Brep {_b}:")
        no_vtx = len(data[_b]["vtx"].keys())
        print (f"\tvtx: {no_vtx}")
        no_edg = len(data[_b]["edg"].keys())
        print (f"\tedg: {no_edg}")
        no_lop = len(data[_b]["lop"].keys())
        print (f"\tlop: {no_lop}")
        no_fac = len(data[_b]["fac"].keys())
        print (f"\tfac: {no_fac}")

        setPt(data[_b]["vtx"], lc)
        setEd(data[_b]["edg"], data[_b]["vtx"], lc )
        setLp(data[_b]["lop"], data[_b]["edg"])
        fid_ls =setFc(data[_b]["fac"],data[_b]["lop"], data[_b]["edg"])
        sfl = gmsh.model.occ.addSurfaceLoop(fid_ls)

        try:
            pv = gmsh.model.occ.addVolume([sfl])
            volumes.append(pv)
        except:
            pass

    if len(data.keys()) > 2 :
        try:
            bu, buu = gmsh.model.occ.fuse([(3, volumes[0])] , [(3, _t) for _t in volumes[1:]], removeObject= False, removeTool= False)
            bs, bss = gmsh.model.occ.fragment([(3,bu[0][1])], [(3, _t) for _t in volumes])
        except:
            pass

        gmsh.model.occ.synchronize()
        for frag in bs:
            gmsh.model.addPhysicalGroup(3, [frag[1]])

    gmsh.model.occ.synchronize()
    gmsh.model.mesh.generate(3)
    save_dir = os.path.join(file_dir, f"{file_name[:-5]}.msh")
    gmsh.write(save_dir)

def setPt(v_dic, lc):
    pt_id  = []
    for  p in v_dic.keys():
        x, y, z = v_dic[p]["pos"]
        _id = gmsh.model.occ.addPoint(x, y, z, lc)
        v_dic[p]["tag"] = _id
        pt_id.append(_id)

def setEd(e_dic, v_dic, lc):

    for e in (e_dic.keys()):
        e_dic[e]["vtx"][0] = gettag(v_dic,[str(e_dic[e]["vtx"][0]),])
        e_dic[e]["vtx"][1] = gettag(v_dic,[str(e_dic[e]["vtx"][1]),])   
        
        if e_dic[e]["degree"] > 1:
            _cp_id =[e_dic[e]["vtx"][0],]

            for idx2 in range(1,len(e_dic[e]["cp"])-1):
                _cp = e_dic[e]["cp"][idx2]
                x, y, z = _cp
                _id = gmsh.model.occ.addPoint(x, y, z, lc)
                _cp_id.append(_id)

             
            _cp_id.append(e_dic[e]["vtx"][1])
            e_id = gmsh.model.occ.addBSpline(_cp_id, degree= e_dic[e]["degree"] , weights=e_dic[e]["w"])
            e_dic[e]["tag"] = [e_id]+[_cp_id]
        else:
                
            e_id = gmsh.model.occ.addLine(e_dic[e]["vtx"][0],e_dic[e]["vtx"][1])
            e_dic[e]["tag"] = [e_id]+[e_dic[e]["vtx"]]
        #print (e_dic[e]["tag"])

def setLp(l_dic, e_dic):

    for l in (l_dic.keys()):
        loop = l_dic[l]
        for t, ed_id in enumerate(loop["edg"]):
            loop["edg"][t] = gettag(e_dic,[str(ed_id),])
        l_id = gmsh.model.occ.addWire( [a[0] for a in loop["edg"]] , checkClosed=True)
        loop["tag"] = [l_id] + loop["edg"]
        #print (loop["tag"])

def setFc(f_dic, l_dic, e_dic):

    fid_ls = []
    for f in (f_dic.keys()):
        face = f_dic[f]
        for t, lop_id in enumerate(face["lop"]):
            face["lop"][t] = gettag(l_dic,[str(lop_id),])

        """fcp_ls = []
        for i in face["lop"][0][1:]:
            for j in i[1]:
                fcp_ls.append(j)
        print (fcp_ls)
        print(face["w"])
                    
        f_id = gmsh.model.occ.addBSplineSurface(
            fcp_ls,numPointsU=face["degree"][0], degreeU = face["degree"][0], degreeV=face["degree"][1], weights=face["w"]
            )"""

        if sum(face["degree"]) > 2:
            f_id = gmsh.model.occ.addBSplineFilling(face["lop"][0][0],)
        else:
            f_id = gmsh.model.occ.addPlaneSurface([a[0] for a in face["lop"]])
        fid_ls.append(f_id)
        face["tag"] = [f_id] + face["lop"]

        #print (face["tag"])
    return fid_ls

def gettag(dic, keyls):

    if len(keyls)   > 1:
        tag = gettag(dic[keyls[0]], keyls[1:])
        
    else:
        tag = dic[keyls[0]]["tag"]
    
    return tag

if __name__ == "__main__":
    main()