import os

current = os.path.dirname(os.path.abspath(__file__))
parent = os.path.abspath(os.path.join(current ,".."))


from ema_timber.communicate.core import Client
from ema_timber.communicate.core import Package

class Wrapper():
    pass