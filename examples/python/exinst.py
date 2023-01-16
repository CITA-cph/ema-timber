import os
import sys
import time

from util import import_ema_timber

import_ema_timber()

date =  time.strftime("%y%m%d")
save_dir = os.path.abspath(f"../ema-timber/examples/python/data/{date}")

import socket
from ema_timber.communicate.core import Telephone

def main():

    #+++++++++++++++++++++++++++++#
    ID = "99"
    HOSTNAME = socket.gethostname()
    IP = socket.gethostbyname(HOSTNAME)
    PORT = 52344 #RECIVING DATA AT
    
    BROADPORT = 55555 #BROADCASTING 

    print (f"\nINSTRUCTOR SERVER : \n\t{HOSTNAME} - {IP}")
    print (f"PORTS : \n\tReceiving - {ID} : {PORT}\n")
    #+++++++++++++++++++++++++++++#
    
    t = Telephone.Telephone(IP, PORT, ID, "SbI")
    t.start()
    
if __name__ == "__main__":
    main()