import os
import sys
import time
import networkx as nx

from util import import_ema_timber
import_ema_timber()

date =  time.strftime("%y%m%d")
save_dir = os.path.abspath(f"./{date}")
print(save_dir)

import socket
import threading

from ema_timber.structure import Structure

def nx_test():
    G = nx.Graph(name="test")
    G.add_node("E0001", some_prop="value stuff", data="some data")

    #print(G.nodes)
    print(G.nodes["E0001"])

    #G["E0001"]["data"] = "some data"

    print(G)

def main():
    s = Structure(name="Demo")

    e0 = s.add_element(data={"beam":{"centreline": None}, "material_data":{"material_name":"SPF", "mat_prop_01":0.5}})
    e1 = s.add_element(data={"beam":{"centreline": None}, "material_data":{"material_name":"SPF", "mat_prop_01":0.5}})
    e2 = s.add_element(data={"data": "some data"})

    s.join([e0, e1], data={"joint_type": "CrossJoint2", "joint_data": {"taper": 0.1, "drilling":{"diameter": 16.0, "countersink": True, "countersink_depth": 12.0}}})


    # Create elements

    # Find element intersections (distance_to_consider_as_intersection)

        # Make edge between intersecting elements (mid_point, t values for both elements as dict)

    # Collapse intersections within radius (radius)

    # Classify joints (T, X, splice, L)

    # Apply joint template (CrossJoint02_Tapered, etc.)

    # Construct joint cutter geometry


    print(s.nodes)

    for n in s.graph:
        print(f"{n} : {s.nodes[n]}")


if __name__=="__main__":
    main()
