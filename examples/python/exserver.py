import os
import sys

current = os.path.dirname(os.path.abspath(__file__))
print (current)
core = os.path.abspath(os.path.join(current ,"../..","src/python/ema_timber/communicate/core"))
print (core)
sys.path.append(core)


import socket
import threading

import Server

def main():

    #+++++++++++++++++++++++++++++#
    HOSTNAME = socket.gethostname()
    HOST = socket.gethostbyname(HOSTNAME)

    BROADPORT = 55555 #BROADCASTING AT
    SERVERPORT = 55556 #RECVING AT
    idserver = "02"
    print (f"SERVER : \n\t{HOSTNAME} - {HOST}")
    print (f"PORTS : \n\tBroadcasting - 01 : {BROADPORT} \n\tServer - {idserver} : {SERVERPORT}\n")
    #+++++++++++++++++++++++++++++#

    threads = []

    # SET UP BROADCAST
    broadcaster = threading.Thread(
        target = Server.protocol.Broadcast.Broadcast,
        args = (HOST, BROADPORT, str(SERVERPORT).encode())
        )
    
    broadcaster.start()
    threads.append(broadcaster)

    S = threading.Thread(
        target = Server.Server,
        args = (HOST, SERVERPORT, idserver, {} , 5)
        )
    S.start()
    threads.append(S)

    for tt in threads:
        tt.join()

if __name__ == "__main__":
    main()