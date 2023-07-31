# THIS FILE SHOULD EXIST IN GRASSHOPPER GHPYTHON

__author__ = "ceep"
__version__ = "2023.03.01"

import Rhino.Geometry as rg
import time
import os
import json

def pointed(c):
    d = {0:{}}
    v = c.Vertices
    e = c.Edges
    l = [list(set(x.EdgeIndices())) for x in v]
    #print (l)
    res, pl = e[l[1][1]].TryGetPlane()
    if res:
        o = pl.Origin
        r1 = rg.Line(o, v[1].Location).Length
        r2 = 1
        h = rg.Line(o, v[0].Location).Length
        d[0]["vtx"] = [o.X, o.Y, o.Z]
        d[0]["rad"] = [r1, r2]
        d[0]["vec"] = [pl.ZAxis.X*h, pl.ZAxis.Y*h, pl.ZAxis.Z*h]
    #print (d)
    return d

def truncated(c):
    d = {0:{}, 1:{}}
    v = c.Vertices
    e = c.Edges
    l = [list(set(x.EdgeIndices())) for x in v]
    
    res1 ,p1 =  e[l[0][0]].TryGetPlane() #0
    res3 ,p3 =  e[l[2][1]].TryGetPlane() #3

    if res1 == res3 == True:
        o1 = p1.Origin
        o2 = p3.Origin
        r1 = rg.Line(o1, v[0].Location).Length
        r2 = rg.Line(o2, v[2].Location).Length
        r3 = 1
        h1 = rg.Line(o1, o2).Length
        h2 = rg.Line(o2, v[1].Location).Length

        d[0]["vtx"] = [o1.X, o1.Y, o1.Z]
        d[0]["rad"] = [r1, r2]
        d[0]["vec"] = [p1.ZAxis.X*h1, p1.ZAxis.Y*h1, p1.ZAxis.Z*h1]

        d[1]["vtx"] = [o2.X, o2.Y, o2.Z]
        d[1]["rad"] = [r2, r3]
        d[1]["vec"] = [p3.ZAxis.X*h2, p3.ZAxis.Y*h2, p3.ZAxis.Z*h2]

    return d

def writeJson(data):
    date =  time.strftime("%y%m%d")
    save_dir = os.path.abspath("./data/"+ date)
    if not os.path.exists(save_dir):
        os.makedirs(save_dir)
    saved = os.path.join(save_dir,"knots.json")
    with open(saved, "w") as f:
        json.dump(data, f,indent=4)
    print (saved)
    return saved

def main():
    k_dic = {}
    for id, _k in enumerate(knots):
        k_dic[id] = {}
        c_ls = [_k, O_r[struct[id*2]], O_r[struct[id*2 +1]]]
        for id2 , c in enumerate(c_ls):
            if c.Vertices.Count > 2:
                k_dic[id][id2] = truncated(c)

            else:
                k_dic[id][id2] = pointed(c)
    writeJson(k_dic)

if run:
    main()
