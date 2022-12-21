import init_examples

import sys
import os
import threading
import socket

from ema_timber.communicate import Client, Server, Instructor, i_sProt
from ema_timber.communicate.Broadcast import tunein

def main():

    #+++++++++++++++++++++++++++++#
    ID = "99"
    HOSTNAME = socket.gethostname()
    IP = socket.gethostbyname(HOSTNAME)
    PORT = 52344 #RECIVING DATA AT
    
    BROADPORT = 55555 #BROADCASTING 

    print (f"\nCLIENT SERVER : \n\t{HOSTNAME} - {IP}")
    print (f"PORTS : \n\tReceiving - {ID} : {PORT}\n")
    #+++++++++++++++++++++++++++++#

    i_s = i_sProt.getprgls()

    threads = []


    T_HOST, T_PORT = tunein(BROADPORT, 0)
    print ("SERVER addr : ", T_HOST, T_PORT)
    
    Client.alive(T_HOST, T_PORT, ID, IP, PORT)

    S = threading.Thread(
        target = Server,
        args = (IP, PORT, ID, i_s)
        )
    S.start()
    threads.append(S)

    operate = threading.Thread(
        target = Instructor,
        args= ( T_HOST, T_PORT, ID, i_s)
        )
    operate.start()
    threads.append(operate)

    for tt in threads:
        tt.join()
if __name__ == "__main__":
    main()