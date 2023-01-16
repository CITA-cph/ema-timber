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
    HOSTNAME = socket.gethostname()
    HOST = socket.gethostbyname(HOSTNAME)

    BROADPORT = 55555 #BROADCASTING AT
    SERVERPORT = 55556 #RECVING AT
    idserver = "02"
    print (f"\nSERVER : \n\t{HOSTNAME} - {HOST}")
    print (f"PORTS : \n\tBroadcasting - 01 : {BROADPORT} \n\tServer - {idserver} : {SERVERPORT}\n")
    #+++++++++++++++++++++++++++++#

    t = Telephone.Telephone(HOST, SERVERPORT, idserver)
    setattr(t, "no_drones", 5)
    t.start()


if __name__ == "__main__":
    main()