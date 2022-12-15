import sys
import os

dir1 = os.path.dirname(os.path.abspath(__file__))
sys.path.append(os.path.dirname(dir1))

import communicate
from communicate import *
from communicate.Broadcast import tunein
import threading
import socket


def main():


    #+++++++++++++++++++++++++++++#
    ID = "04"
    HOSTNAME = socket.gethostname()
    IP = socket.gethostbyname(HOSTNAME)
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
