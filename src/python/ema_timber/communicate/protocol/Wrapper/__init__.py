import os
import sys

current = os.path.dirname(os.path.abspath(__file__))
#print (current)
parent = os.path.abspath(os.path.join(current ,".."))
#print(parent)
#sys.path.append(parent)

from ema_timber.communicate.core import Client
from ema_timber.communicate.core import Package

class Wrapper():
    pass