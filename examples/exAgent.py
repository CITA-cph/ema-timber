import sys
import os
import threading
import socket
import init_examples
import time

from ema_timber.communicate import Client
from ema_timber.communicate import Server
from ema_timber.communicate import c_sProt
from ema_timber.communicate.Broadcast import tunein

def alive(BROADPORT,TYPE, ID, IP, PORT):
    delta  = "c"
    while True:
        # LISTEN TO BC
        T_HOST, T_PORT = tunein(BROADPORT, TYPE)
        if T_HOST and T_HOST:
            if delta != "a":
                print ("SERVER addr : ", T_HOST, T_PORT)
                # GIVE ADDR
                Client.PING(T_HOST, T_PORT, ID, IP, PORT, False)
            time.sleep(5)
            delta = "a"
        else:
        
            if delta !="b":
                print ("No connection to server")
            time.sleep(2)
            delta = "b"

def main():


    #+++++++++++++++++++++++++++++#
    ID = "04"
    TYPE = 1
    HOSTNAME = socket.gethostname()
    IP = socket.gethostbyname(HOSTNAME)
    # If you are using a PI use HOSTNAME + ".local"
    PORT = 55444 #RECIVING DATA AT
    
    BROADPORT = 55555 #BROADCASTING 

    print (f"\nCLIENT SERVER : \n\t{HOSTNAME} - {IP}")
    print (f"PORTS : \n\tReceiving - {ID} : {PORT}\n")
    #+++++++++++++++++++++++++++++#

    c_s = c_sProt.getprgls()

    threads = []
    
    S = threading.Thread(
        target = Server,
        args = (IP, PORT, ID, c_s)
        )
    S.start()
    threads.append(S)

    ALIVE = threading.Thread(
        target = alive,
        args = (BROADPORT, TYPE, ID, IP, PORT)
    )
    ALIVE.start()
    threads.append(ALIVE)

    for tt in threads:
        tt.join()

if __name__=="__main__":
    main()
