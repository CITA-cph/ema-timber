import sys
import os
import threading
import socket
import init_examples

from ema_timber.communicate import Client
from ema_timber.communicate import Server
from ema_timber.communicate import c_sProt
from ema_timber.communicate.Broadcast import tunein



def main():


    #+++++++++++++++++++++++++++++#
    ID = "04"
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
    
    # LISTEN TO BC
    T_HOST, T_PORT = tunein(BROADPORT, 0)
    print ("SERVER addr : ", T_HOST, T_PORT)

    # GIVE RECV ADDR
    Client.PING(T_HOST, T_PORT, ID, IP, PORT)

    S = threading.Thread(
        target = Server,
        args = (IP, PORT, ID, c_s)
        )
    S.start()
    threads.append(S)

    for tt in threads:
        tt.join()

if __name__=="__main__":
    main()
