import json

from OCC.Display.SimpleGui import init_display

import specklepy
from specklepy.serialization.base_object_serializer import BaseObjectSerializer

from geometry.convert import SpeckleToOcc

print(dir(SpeckleToOcc))

def main(speckle_path: str) -> None:

    with open(speckle_path, 'r') as file:
        contents = file.read()

        blob = json.reads(contents)

        print(blob)

    serializer = BaseObjectSerializer()
    obj_id, obj = serializer.traverse_base(speckle_object)


    speckle_brep = None
    brep = SpeckleToOcc.brep(speckle_brep)

    # Visualize results
    display, start_display, add_menu, add_function_to_menu = init_display()
    display.DisplayShape(brep, update=True)
    start_display()



if __name__=="__main__":
    main("C:/tmp/speckle.json")