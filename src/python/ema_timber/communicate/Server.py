# SERVER CHANNEL
import socket
import time
import numpy as np
from . import Processor
import cv2 
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
                print(f'\n[- {self.id} -] - Connected to :', addr)
                data = self.recvdata(c) # GETTING DATA
                
                if data:
                    if data == b"PING":
                        self.updatePages(c)
                        print("Terminating :", addr)
                        continue
                    elif data ==b"FILE":
                        self.recvBytestream(c)
                        print("Terminating :", addr)
                        continue
                    c.recv(1024) # CLOSING
                    print("Terminating :", addr)
                    self.processor.postJob(data)

            except:
                c.close()
                raise
                print("DATA ERROR")

    def updatePages(self, c):

        p_ID = self.recvdata(c).decode()
        p_IP = self.recvdata(c).decode()
        p_PORT = int(self.recvdata(c).decode())
        c.recv(1024) # CLOSING
        self.processor.pages.set_address({p_ID:[p_IP, p_PORT]})
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
        c.recv(1024) # CLOSING
        y = np.frombuffer(data, dtype=np.uint8).reshape(1944,3264)
        print (y)
        cv2.imshow("window", y)
        cv2.waitKey(0)
        cv2.destroyAllWindows()


