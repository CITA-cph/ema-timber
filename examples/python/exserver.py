import os
import sys
import time

from util import import_ema_timber

import_ema_timber()

date =  time.strftime("%y%m%d")
save_dir = os.path.abspath(f"../ema-timber/examples/python/data/{date}")

import socket
import threading

from ema_timber.communicate import protocol
from ema_timber.communicate.core import Client, Server


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

    threads = []

    # SET UP BROADCAST
    broadcaster = threading.Thread(
        target = protocol.Broadcast.Broadcast,
        args = (HOST, BROADPORT,"s",str(SERVERPORT).encode())
        )
    
    broadcaster.start()
    threads.append(broadcaster)

    S = threading.Thread(
        target = Server,
        args = (HOST, SERVERPORT, idserver, {} , 5, save_dir)
        )
    S.start()
    threads.append(S)

    for tt in threads:
        tt.join()

if __name__ == "__main__":
    main()