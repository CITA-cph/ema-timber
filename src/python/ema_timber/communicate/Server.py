# SERVER CHANNEL
import socket
import time
import numpy as np
from . import Processor
import os
import copy

def makeDir(parentpath):
    if not os.path.exists(parentpath):
        os.makedirs(parentpath)

date =  time.strftime("%y%m%d")
base_dir = os.path.abspath(f"../ema-timber/examples/{date}")
makeDir(base_dir)

class Server():

    def __init__(self, HOST, PORT, id, prgls):
        
        self.IP  = HOST
        self.PORT = PORT
        self.id  = id
        self.processor = Processor(prgls)
        self.processor.pages.set_address({self.id:[self.IP, self.PORT]})
        self.sock = self.socketSetup()
        if self.sock:
            self.channel()

    def socketSetup(self):

        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    
        try:
            s.bind((self.IP, self.PORT))
            time.sleep(0.05)
            print(f"[- {self.id} -] binded to port:{self.IP} {self.PORT}")
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
                    elif data ==b"FILE":
                        print(f'\n[- {self.id} -] - Connected to :', addr)
                        self.recvBytestream(c)
                        print("Terminating :", addr)
                        continue
                    c.recv(1024) # CLOSING
                    print(f'\n[- {self.id} -] - Connected to :', addr)
                    print("Terminating :", addr)
                    self.processor.postJob(data)

            except:
                c.close()
                print("DATA ERROR")

    def updatePages(self, c):

        p_ID = self.recvdata(c).decode()
        p_IP = self.recvdata(c).decode()
        p_PORT = int(self.recvdata(c).decode())
        c.recv(1024) # CLOSING
        p0 = copy.copy(self.processor.pages.address)
        self.processor.pages.set_address({p_ID:[p_IP, p_PORT]})
        if p0 != self.processor.pages.address:

            print (self.processor.pages.address)

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

        dst = self.recvdata(c).decode().split("/") #parent/subname/
        c.recv(1024) #CLOSING

        folder_dir = os.path.join(base_dir, dst[0],dst[1])
        makeDir(folder_dir)
        print (f"saved to {folder_dir}")

        output_dir = os.path.join(folder_dir, dst[1] + "_" + dst[2] + ".npy")
        np.save (output_dir,y)


