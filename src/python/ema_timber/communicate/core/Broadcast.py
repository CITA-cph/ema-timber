# BROADCASTING CHANNEL
import socket
import time
from . import Client

class Broadcast(Client):

    def __init__(self, HOST= "127.0.0.1", PORT= 55555,  MESSAGE=b"WOOD IS COOL"):      
        self.HOST = HOST
        self.BROADPORT = PORT
        self.MESSAGE = MESSAGE
        self.kill = False
        self.T_HOST = None
        self.T_PORT = None

    def startBroadcast(self, bc_mode = "B", message = None):
        if  message:
            self.MESSAGE = message
        if bc_mode == "B":
            self.broadcasting()
            print ("broadcast - end")
        elif bc_mode == "b":
            self.tunein()
            print ("DEAD")

    def broadcasting(self):
        
        server = socket.socket(socket.AF_INET, socket.SOCK_DGRAM, socket.IPPROTO_UDP)
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
        server.settimeout(0.2)
        print(f"Broadcasting [ {self.HOST} : {self.BROADPORT} ] : {self.MESSAGE.decode()}")

        while not self.kill:
            server.sendto(self.MESSAGE, ("<broadcast>", self.BROADPORT))
            time.sleep(1)
        
        return

    def tunein(self):

        client = socket.socket(socket.AF_INET, socket.SOCK_DGRAM, socket.IPPROTO_UDP)  # UDP
        client.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        client.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
        client.settimeout(4)
        print ("waiting for broadcast...")
        
        while not self.kill:
            try:
                client.bind(("", self.BROADPORT))
                data, addr = client.recvfrom(1024)
                print ("listening to broadcast")
                try:
                    T_HOST =  addr[0]
                    T_PORT = int(data.decode())
                except: 
                    T_HOST =  addr[0]
                    T_PORT = data.decode()

                self.T_HOST = T_HOST
                self.T_PORT = T_PORT
                break
            except Exception as e:
                #print (e)
                #print (f"Failed to reach broadcast at {PORT}")
                continue        
        
        i, h, p = self.MESSAGE
        if not self.kill:
            self.ALIVE(T_HOST, T_PORT, i , h, p)

        client.close()

    def ALIVE(self, T_HOST, T_PORT, i , h, p):
        delta  = 0
        while not self.kill:
            res = self.PING(T_HOST, T_PORT, i, h, p, False)
            if res:
                if delta > 0:
                    print  ("\nReconnected to server")
                delta = 0
                time.sleep(5)
            else:
                if delta == 0:
                    print ("\nLost connection to server")
                time.sleep(2)
                delta += 1