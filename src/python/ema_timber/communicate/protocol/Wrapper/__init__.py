import os
import sys

current = os.path.dirname(os.path.abspath(__file__))
core = os.path.abspath(os.path.join(current ,"../../","core"))
sys.path.append(core)

import Client
import Package

class Wrapper():
    pass
if __name__ == "__main__":
    print (current)
    print (core)
    print (dir(Client))
    print (dir(Package))