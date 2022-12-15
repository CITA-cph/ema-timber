import sys
import os
import socket

dir1 = os.path.dirname(os.path.abspath(__file__))
sys.path.append(os.path.dirname(dir1))

import communicate
from communicate import *
from communicate.Broadcast import tunein
from communicate.Package import pack

BROADPORT = 55555 #BROADCASTING AT
ID = "99"
HOSTNAME = socket.gethostname()
IP = socket.gethostbyname(HOSTNAME)
PORT  = 55544
T_HOST, T_PORT = tunein(BROADPORT, 0)
print (T_HOST, T_PORT)
Client.PING(T_HOST, T_PORT, ID, IP, PORT)
message  = pack(TASK = "takeImg", args=[ID])
s = Client.clientOut(T_HOST, T_PORT, message)
