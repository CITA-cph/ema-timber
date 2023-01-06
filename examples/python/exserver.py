import os
import sys
import time 

date =  time.strftime("%y%m%d")
current = os.path.dirname(os.path.abspath(__file__))
print (current)
core = os.path.abspath(os.path.join(current ,"../..","src/python/ema_timber/communicate/core"))
print (core)
sys.path.append(core)
save_dir = os.path.abspath(f"../ema-timber/examples/python/{date}")

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
    print (f"\nSERVER : \n\t{HOSTNAME} - {HOST}")
    print (f"PORTS : \n\tBroadcasting - 01 : {BROADPORT} \n\tServer - {idserver} : {SERVERPORT}\n")
    #+++++++++++++++++++++++++++++#

    threads = []

    # SET UP BROADCAST
    broadcaster = threading.Thread(
        target = Server.protocol.Broadcast.Broadcast,
        args = (HOST, BROADPORT,"s",str(SERVERPORT).encode())
        )
    
    broadcaster.start()
    threads.append(broadcaster)

    S = threading.Thread(
        target = Server.Server,
        args = (HOST, SERVERPORT, idserver, {} , 5, save_dir)
        )
    S.start()
    threads.append(S)

    for tt in threads:
        tt.join()

if __name__ == "__main__":
    main()