import os
import sys

current = os.path.dirname(os.path.abspath(__file__))
#print (current)
parent = os.path.abspath(os.path.join(current ,".."))
#print(parent)
#sys.path.append(parent)

from core import Client
from core import Package

class Wrapper():
    pass