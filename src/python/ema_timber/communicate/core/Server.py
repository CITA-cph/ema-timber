import os
import sys

current = os.path.dirname(os.path.abspath(__file__))
parent = os.path.dirname(current)
sys.path.append(parent)

import copy
import socket
import time

import numpy as np
import Processor
import protocol

def main( HOST, PORT, id, prgls ):
    s = Server( HOST, PORT, id, prgls)

def makeDir(parentpath):
    if not os.path.exists(parentpath):
        os.makedirs(parentpath)

date =  time.strftime("%y%m%d")
base_dir = os.path.abspath(f"../ema-timber/examples/{date}")
makeDir(base_dir)


class Server(Processor.Processor):

    def __init__(self, HOST, PORT, id, prgls = {} , no_drones = 1):
        
        self.IP  = HOST
        self.PORT = PORT
        self.id  = id
        self.prgls = prgls | protocol.getmods()
        Processor.Processor.__init__(self,id,self.prgls, no_drones = no_drones)
        self.set_address({self.id:[self.IP, self.PORT]})
        self.sock = self.socketSetup()
        if self.sock:
            self.channel()

    def socketSetup(self):

        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    
        try:
            s.bind((self.IP, self.PORT))
            time.sleep(0.05)
            print(f"[- {self.id} -] binded to port: {self.IP} {self.PORT}")
            s.listen()
            print(f"[- {self.id} -] is listening" )
            return s
        except:
            print (f"[- {self.id} -] could not bind to port:{self.IP} {self.PORT}")
            return False

    def recvdata(self, c):
        try:
            data = c.recv(1024)
            c.send(b"received")
            return data
        except:
            print("Could not receive data")
            return False

    def channel(self):
        while True:
            try:
                c, addr = self.sock.accept()
                
                data = self.recvdata(c) # GETTING DATA
                
                if data:

                    if data == b"PING":
                        self.updatePages(c)
                        #print("Terminating :", addr)
                        continue

                    elif data == b"FILE":
                        print(f'\n[- {self.id} -] - Connected to :', addr)
                        self.recvBytestream(c)
                        print("Terminating :", addr)
                        continue
                    
                    elif data == b"MODS":
                       self.send_modls(c)
                       continue


                    c.recv(1024) # CLOSING
                    print(f'\n[- {self.id} -] - Connected to :', addr)
                    print("Terminating :", addr)
                    self.postJob(data)

            except Exception as e:
                c.close()
                print (e)
                print("DATA ERROR")

    def updatePages(self, c):

        p_ID = self.recvdata(c).decode()
        p_IP = self.recvdata(c).decode()
        p_PORT = int(self.recvdata(c).decode())
        c.recv(1024) # CLOSING
        p0 = copy.copy(self.address)
        self.set_address({p_ID:[p_IP, p_PORT]})
        if p0 != self.address:

            print (self.address)

    def recvBytestream(self,c):

        size = int(self.recvdata(c).decode())
        print(f"waiting for {size} bytes")

        # receive all bytes
        data = bytearray()
        while len(data) < size:
            packet = c.recv(size - len(data))
            if not packet:
                return None
            data.extend(packet)

        print(f"received {len(data)} bytes")
        c.send(f"received {size} bytes".encode() )
        y = np.frombuffer(data, dtype=np.uint8)

        tmp = self.recvdata(c).decode()
        dst  = tmp.split("/") #parent/subname/
        c.recv(1024) #CLOSING

        folder_dir = os.path.join(base_dir, dst[0],dst[1])
        makeDir(folder_dir)
        print (f"saved to {folder_dir}")

        output_dir = os.path.join(folder_dir, dst[1] + "_" + dst[2] + ".npy")
        np.save (output_dir,y)
    
    def send_modls(self , c):
        c.recv(1024)
        l = list(self.prgls.keys())
        c.send(str(len(l)).encode())
        c.recv(1024)
        for k in list(self.prgls.keys()):
            c.send(k.encode())
            c.recv(1024)
        c.recv(1024) # CLOSING
if __name__ == "__main__":
    
    HOSTNAME = socket.gethostname()
    HOST = socket.gethostbyname(HOSTNAME)
    PORT = 55556
    id = "02"
    ls = {}
    Server(HOST, PORT, id, ls)