import sys
import os

dir1 = os.path.dirname(os.path.abspath(__file__))
sys.path.append(os.path.dirname(dir1))

import communicate
from communicate import *
import socket
import threading

def main():


    #+++++++++++++++++++++++++++++#
    HOSTNAME = socket.gethostname()
    HOST = socket.gethostbyname(HOSTNAME)

    BROADPORT = 55555 #BROADCASTING AT
    INSTPORT = 55556 #RECVING INSTRUCTIONS AT
    CLIENTPORT = 55557 #RECIVING DATA AT
    idinst = "01"
    idclient = "02"
    print (f"\nSERVER : \n\t{HOSTNAME} - {HOST}")
    print (f"PORTS : \n\tBroadcasting - 00 : {BROADPORT} \n\tInstructor - {idinst} : {INSTPORT} \n\tClient - {idclient} : {CLIENTPORT}\n")
    #+++++++++++++++++++++++++++++#

    i_s = i_sProt.getprgls()
    c_s = c_sProt.getprgls()

    threads = []

    # SET UP BROADCAST
    broadcaster = threading.Thread(
        target = Broadcast,
        args = (HOST, BROADPORT, str(INSTPORT).encode() + str(CLIENTPORT).encode())
        )
    
    broadcaster.start()
    threads.append(broadcaster)

    S = threading.Thread(
        target = Server,
        args = (HOST, INSTPORT, idinst, i_s)
        )
    S.start()
    threads.append(S)

    S = threading.Thread(
        target = Server,
        args = (HOST, CLIENTPORT, idclient, c_s)
        )
    S.start()
    threads.append(S)

    for tt in threads:
        tt.join()

if __name__=="__main__":
    main()

