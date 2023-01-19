import os
import time

from util import import_ema_timber

import_ema_timber()

date =  time.strftime("%y%m%d")
save_dir = os.path.abspath(f"../ema-timber/examples/python/data/{date}")

import socket
from ema_timber.communicate.core import Telephone


def main():


    #+++++++++++++++++++++++++++++#
    ID = "10"
    HOSTNAME = socket.gethostname()
    IP = socket.gethostbyname(HOSTNAME + ".local")
    # If you are using a PI use HOSTNAME + ".local"
    PORT = 55444 #RECIVING DATA AT
    
    print (f"\nCLIENT SERVER : \n\t{HOSTNAME} - {IP}")
    print (f"PORTS : \n\tReceiving - {ID} : {PORT}\n")
    #+++++++++++++++++++++++++++++#

    t = Telephone.Telephone(IP, PORT, ID, "Sbi")
    t.start()

if __name__=="__main__":
    main()
