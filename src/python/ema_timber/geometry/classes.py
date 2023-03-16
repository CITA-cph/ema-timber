import json
import logging
from pydantic import BaseModel
from typing import Tuple, List

class Plane(BaseModel):
    """
    Plane class
    """    
    origin: Tuple[float, float, float] = (0,0,0)
    xaxis: Tuple[float, float, float] = (1,0,0)
    yaxis: Tuple[float, float, float] = (0,1,0)

class Board(BaseModel):
    """
    Board class for storing basic parameters
    """
    id: str = "Board"
    basePlane: Plane = Plane()
    dimensions: Tuple[float, float, float] = (0,0,0)

class KnotRegion(BaseModel):
    t : float = 1.0
    radius1 : float = 0
    radius2 : float = 0

class Knot(BaseModel):
    """
    Knot class for storing basic parameters
    """
    id: str = "Knot"
    start: Tuple[float, float, float] = (0,0,0)
    direction: Tuple[float, float, float] = (0,0,1)
    radius: float = 0
    fdr: KnotRegion = KnotRegion()
    tr: KnotRegion = KnotRegion()
    dead_knot_radius: float = 0.0

    """        
    KR: [ A, B ]

            B----->
        ___________
        \    ^ A  /
         \   |   /
          \  |  /
           \ | /
            \|/

    FDR & TR (a): [ A, B, C, D, E, F ]

            F----->
        ___________
        \    ^ E  /
         \   |   /
          \  |  /
           \ | /
            \|/

    FDR & TR (b):

             D--------->
    ___________________
    |        ^ C      |
     \       |       /
      |      |B---->|
       \_____|_____/
        \    ^ A  /
         \   |   /
          \  |  /
           \ | /
            \|/     

    """
class Log(BaseModel):
    """
    Log class for storing basic parameters
    """
    id: str = "Log"
    basePlane: Plane = Plane()
    pith: List[Tuple[float, float, float]] = []
    knots: List[Knot] = []
    cone_angle : float = 0.0
    spiral_grain_angle: float = 0.0
    radius0 : float = 0.0
    radius1 : float = 0.0

def assemble_log_data(cone_path, pith_path):

    knots = []
    counter = 0
    with open(cone_path, "r") as file:
        for line in file:
            tok = line.split(',')
            k = Knot(id=f"knot_{counter:02d}", start=(float(tok[0]), float(tok[1]), float(tok[2])),
                direction=(float(tok[3]), float(tok[4]), float(tok[5])),
                radius=float(tok[6]))
            knots.append(k)
            counter += 1

    pith = []
    with open(pith_path, "r") as file:
        for line in file:
            tok = line.split(',')
            pith.append((float(tok[0]), float(tok[1]), float(tok[2])))

    return Log(knots=knots, pith=pith)

def main():
    logging.basicConfig(level=logging.DEBUG)

    import os
    os.chdir("../../../../examples/data")
    print(os.getcwd())

    print("Creating board...", end="")
    board = Board(id="sec_1_76.9677", dimensions = (1000, 100, 40))
    print("OK.")

    knot_path = "ConeData.txt"
    pith_path = "Pith.txt"

    print(f"Assembling log data...", end="")
    log = assemble_log_data(knot_path, pith_path)
    print("OK.")

    print(f"Writing board data...", end="")
    with open("sample_board.json", "w") as file:
        file.write(board.json(indent=2))
        # file.write(operations.serialize(board))
    print("OK.")

    print(f"Writing log data...", end="")
    with open("sample_log.json", "w") as file:
        file.write(log.json(indent=2))
        # file.write(operations.serialize(log))
    print("OK.")

    from specklepy.objects import Base
    base = Base(data=log)
    print(base)
    print(base.data.pith)

    for knot in base.data.knots:
        print(knot)

if __name__=="__main__":
    main()

