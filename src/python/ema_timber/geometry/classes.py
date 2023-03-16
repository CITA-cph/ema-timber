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
    Board class for storing basic parameters.

    id : Arbitrary ID tag.

    basePlane : The base plane of the board, where
    the bottom left corner of the board lies on 
    (0,0,0) and the board extends into the positive
    X-, Y-, and Z-quadrants.

    dimensions : The dimensions of the board in 
    the positive X, Y, and Z axes.
    """
    id: str = "Board"
    basePlane: Plane = Plane()
    dimensions: Tuple[float, float, float] = (0,0,0)

class KnotRegion(BaseModel):
    """
    Class for representing a fibre deviation region 
    and transition region around knots.

    If t == 1, then it is a simple cone with length
    of the parent knot and radius = radius1.

    Otherwise, it is a composite of a cone and 
    truncated cone, where cone radius = radius1
    and the truncated cone uses both radii. The 
    parameter t is the ratio of the total knot
    length of the cone and truncated cone.
    """
    t : float = 1.0
    radius1 : float = 0
    radius2 : float = 0

class Knot(BaseModel):
    """
    Knot class for storing basic parameters
    as well as the fibre deviation and 
    transition regions, if available.

    The length of the knot is the magnitude
    of the direction vector.

    start : The XYZ point of the knot cone vertex.

    direction : The axis of the knot cone, NOT normalized.

    radius : Radius of the knot cone.

    fdr : Fibre deviation region

    tr : Transition region

    dead_knot_radius : Currently unused.
    """
    id: str = "Knot"
    start: Tuple[float, float, float] = (0,0,0)
    direction: Tuple[float, float, float] = (0,0,1)
    radius: float = 0
    fdr: KnotRegion = KnotRegion()
    tr: KnotRegion = KnotRegion()
    dead_knot_radius: float = 0.0

    """        
    KR: [ A, B ] # Knot region

            B----->
        ___________
        \    ^ A  /
         \   |   /
          \  |  /
           \ | /
            \|/

    FDR & TR (a): [ A, B, C, D, E, F ] 
    # Fibre deviation region and 
    # Transition region

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

    basePlane : The base plane of the log. The 
    log extends in the positive Z-axis and it
    is roughly centred on the plane origin.

    pith : List of XYZ values that 
    define the vertices of a polgyonal 
    pith line.

    knots : List of Knot objects.

    cone_angle : Conical angle of the log.

    radius0 : Overall radius of the log at its base.

    radius1 : Overall radius of the log at its tip.
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
    """
    Assemble a Log object and associated Knot list
    from text files for each.

    cone_path : Path of the text file describing 
    knot cones. Each line contains comma-separated floats, 
    for each basic knot parameter (startX, startY, 
    startZ, directionX, directionY, directionZ, radius).

    pith_path : Path of the text file describing the
    pith line. Each line contains comma-separated floats,
    for each segment in the pith line (sX, sY, sZ, eX, eY, eZ).
    """

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

