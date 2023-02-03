# THIS FILE SHOULD EXIST IN GRASSHOPPER GHPYTHON
__author__ = "ceep"
__version__ = "2023.02.02"


import json
import os
import time

def main():

    date =  time.strftime("%y%m%d")
    save_dir = os.path.abspath("./data/"+ date)
    print (save_dir)
    if not os.path.exists(save_dir):
        os.makedirs(save_dir)

    j_Brep = {}
    for b_id, _B in enumerate(solid):

        j_Brep[b_id] = {"vtx":{}, "edg":{}, "lop":{}, "fac":{}}

        _v_ls = [v for v in _B.Vertices]
        e_ls = _B.Edges
        l_ls = _B.Loops
        f_ls = _B.Faces

        for _v in _v_ls:
            j_Brep[b_id]["vtx"][_v.ComponentIndex().Index] = {"pos":[_v.Location.X, _v.Location.Y, _v.Location.Z]}
        
        for e in e_ls:
            stp_id = e.StartVertex.ComponentIndex().Index
            end_id = e.EndVertex.ComponentIndex().Index
            sgm = e.DuplicateSegments()
            D = sgm[0].Degree

            j_Brep[b_id]["edg"][e.ComponentIndex().Index] = {"vtx":[stp_id, end_id], "degree":D}

            if D > 1:
                e_cp = []
                e_w = []
                for cp in sgm[0].Points:
                    e_cp.append([cp.Location.X, cp.Location.Y, cp.Location.Z])
                    e_w.append(cp.Weight)
                j_Brep[b_id]["edg"][e.ComponentIndex().Index].update({"cp":e_cp, "w":e_w})

        for l in l_ls:
            j_Brep[b_id]["lop"][l.ComponentIndex().Index] = {"edg":[a.Edge.ComponentIndex().Index for a in l.Trims]}

        for f in f_ls:

            nf = f.ToNurbsSurface()
            dU = nf.Degree(0)
            dV = nf.Degree(1)
            f_w = [cp.Weight for cp in nf.Points]

            j_Brep[b_id]["fac"][f.ComponentIndex().Index] = {"lop":[a.ComponentIndex().Index for a in f.Loops], "edg": [a for a in f.AdjacentEdges()], "degree":[dU, dV] , "w":f_w}
    saved = os.path.join(save_dir,"model.json")
    with open(saved, "w") as f:
        json.dump(j_Brep, f,indent=4)
    return saved

if run:
    saved = main()