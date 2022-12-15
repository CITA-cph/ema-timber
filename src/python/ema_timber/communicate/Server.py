# SERVER CHANNEL
import socket
import time

from . import Processor

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
            print(f"socket {self.id} binded to port:{self.IP} {self.PORT}")
            s.listen()
            print(f"socket {self.id} is listening" )
            return s
        except:
            print (f"socket {self.id} could not bind to port:{self.IP} {self.PORT}")
            return False

    def recvdata(self, c):
        try:
            data = c.recv(1024)
            c.send(b"recieved")
            return data
        except:
            print("Could not recieve data")
            return False

    def channel(self):
        while True:
            try:
                c, addr = self.sock.accept()
                print(f'\nS {self.id} - Connected to :', addr)
                data = self.recvdata(c) # GETTING DATA
                
                if data:
                    if data == b"PING":
                        self.updatePages(c)
                        print("Terminating :", addr)
                        continue
                    c.recv(1024) # CLOSING
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
        self.processor.pages.set_address({p_ID:[p_IP, p_PORT]})
        print (self.processor.pages.address)