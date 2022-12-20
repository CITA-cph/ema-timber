import init_examples
import os
from datetime import datetime
from ema_timber.structure import Structure

'''
Example of a Structure object used to
put elements and joints into a graph.
Includes reading and writing.
'''

def main():

    s = Structure(name="Test", attr={"client":"Jim", "location":"the Moon", "created": datetime.now().strftime("%Y-%m-%d %H:%M:%S")})
    s.add_element("test1")
    s.add_element("test2")

    s.join(["test1", "test2"])
    s.join(["test1", "test3"])

    s.add_element("B03", {"type":"SingleCurvedGlulam", "cost":1300})
    s.add_element("B04", {"type":"DoubleCurvedGlulam", "cost":2500})
    s.add_element("B05", {"type":"SingleCurvedGlulam", "cost":1700})

    print(f"All nodes: {s.nodes}")
    print(f"Looking for node B03: {s.nodes['B03']}")
    print(f"Client is '{s['client']}'")
    print(f"Structure metadata is:")
    for key in s.keys():
        print(f"    {key}: {s[key]}")


    s.join(["B03", "B04", "B05"])

    path = os.path.abspath("./tmp/graph.json")
    if not os.path.exists(os.path.dirname(path)):
        os.mkdir(os.path.dirname(path))

    s.write(path)
    s2 = Structure.read(path)

    print(f"Graphs are equal: {s == s2}")

if __name__=="__main__":
    main()