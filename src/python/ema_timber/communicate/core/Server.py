import os
import copy
import socket
import time
import numpy as np
from . import  Yellowpages

def makeDir(parentpath):
    if not os.path.exists(parentpath):
        os.makedirs(parentpath)

class Server():
    def __init__(self, HOST = "127.0.0.1", PORT= 55553, ID="898", loc = "HERE", prgls = {}):
        self.HOST = HOST
        self.PORT = PORT
        self.id = ID
        self.base_dir = loc
        self.TASKls = []
        self.prgls = prgls
        self.book = Yellowpages(self.id)
    
    def setupServer(self):
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        try:
            s.bind((self.HOST, self.PORT))
            time.sleep(0.05)
            print(f"[- {self.id} -] binded to port: {self.HOST} {self.PORT}")
            s.listen()
            print(f"[- {self.id} -] is listening" )
            return s
        except:
            print (f"[- {self.id} -] could not bind to port:{self.HOST} {self.PORT}")
            return False

    def startServer(self, s_type = "S"):
        self.sock = self.setupServer()
        if self.sock:
            self.book = Yellowpages(self.id, {self.id : [self.HOST, self.PORT]})
            self.book.set_address()
            self.channel(s_type)
        else:
            print ("Server failed to start")
        return False
    
    def recvdata(self, c):
        try:
            data = c.recv(1024)
            c.send(b"received")
            return data
        except:
            print("Could not receive data")
            return False

    def channel(self, s_type):
        while not self.kill:
            try:
                self.sock.settimeout(4)
                c, addr = self.sock.accept()
                self.sock.settimeout(99)
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
                    if s_type == "S":
                        self.TASKls.append(data)
                    elif s_type == "s":
                        pass

            except socket.timeout:
                continue
            except Exception as e:
                print (e)
                print("DATA ERROR")
        
        print ("server -end")
        return

    def updatePages(self, c):

        p_ID = self.recvdata(c).decode()
        p_IP = self.recvdata(c).decode()
        p_PORT = int(self.recvdata(c).decode())
        c.recv(1024) # CLOSING
        p0 = copy.copy(self.book.address)
        self.book.set_address({p_ID:[p_IP, p_PORT]})
        if p0 != self.book.address:

            print (self.book.address)
            

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

        folder_dir = os.path.join(self.base_dir, dst[0],dst[1])
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