import os
import sys

current = os.path.dirname(os.path.abspath(__file__))
print (current)
core = os.path.abspath(os.path.join(current ,"../..","src/python/ema_timber/communicate/core"))
print (core)
sys.path.append(core)

import socket
import threading
import time

import Server, Client

def main():


    #+++++++++++++++++++++++++++++#
    ID = "10"
    HOSTNAME = socket.gethostname()
    IP = socket.gethostbyname(HOSTNAME)
    # If you are using a PI use HOSTNAME + ".local"
    PORT = 55444 #RECIVING DATA AT
    
    BROADPORT = 55555 #BROADCASTING 

    print (f"\nCLIENT SERVER : \n\t{HOSTNAME} - {IP}")
    print (f"PORTS : \n\tReceiving - {ID} : {PORT}\n")
    #+++++++++++++++++++++++++++++#

    threads = []
    
    S = threading.Thread(
        target = Server.Server,
        args = (IP, PORT, ID)
        )
    S.start()
    threads.append(S)

    # LISTEN TO BC
    run = True
    delta = 0
    while run:
        post = Server.protocol.Broadcast.Broadcast(PORT = BROADPORT, task  = "l")
        T_HOST, T_PORT = post.addr, post.message
        if T_HOST:
            run = False
            delta = 0
            print (f"Server at {T_HOST} {T_PORT}")
        else:
            if delta == 0:
                print (f"No broadcast at {BROADPORT}")
            delta += 1 
        time.sleep(2)

    alive = threading.Thread(
        target= Client.ALIVE,
        args= (T_HOST, T_PORT, ID, IP, PORT)
    )
    alive.start()
    threads.append(alive)

    for tt in threads:
        tt.join()

if __name__=="__main__":
    main()
