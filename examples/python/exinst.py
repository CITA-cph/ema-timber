import os
import sys
import time

from util import import_ema_timber

import_ema_timber()

date =  time.strftime("%y%m%d")
save_dir = os.path.abspath(f"../ema-timber/examples/python/{date}")
print (save_dir)

import socket
import threading
import time

from ema_timber.communicate.core import Client, Instructor, Server
from ema_timber.communicate import protocol


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


    threads = []


    S = threading.Thread(
        target = Server,
        args = (IP, PORT, ID, {} , 1 , save_dir)
        )
    S.start()
    threads.append(S)

    # LISTEN TO BC
    run = True
    delta = 0
    while run:
        post = protocol.Broadcast.Broadcast(PORT = BROADPORT, task  = "l")
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

    operate = threading.Thread(
        target = Instructor,
        args= (T_HOST, T_PORT, ID)
        )
    operate.start()
    threads.append(operate)

    for tt in threads:
        tt.join()
if __name__ == "__main__":
    main()