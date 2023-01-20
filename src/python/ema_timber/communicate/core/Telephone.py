import threading
import os 
import sys
import time

current = os.path.dirname(os.path.abspath(__file__))
parent = os.path.dirname(current)
date =  time.strftime("%y%m%d")
base_dir = os.path.abspath(f"../ema-timber/examples/python/data/{date}")

from . import Instructor, Server, Broadcast, Processor
import ema_timber.communicate.protocol as ema_protocol

class Telephone (threading.Thread, Server, Processor, Instructor, Broadcast):
    
    def __init__ (self,HOST = "127.0.0.1", PORT = 55554, id = "05", TYPE = "SBi", loc = base_dir ):
        threading.Thread.__init__(self)
        Broadcast.__init__(self)
        Server.__init__(self)
        Processor.__init__(self, self.TASKls, book = self.book)
        Instructor.__init__(self)
        self.base_dir = loc
        self.HOST = HOST
        self.PORT = PORT
        self.id = id
        self.kill = False
        self.TYPE = TYPE
        self.prgls = ema_protocol.modules
        self.promptls = {"kill": self.killall, "about": self.about, "jobs": self.jobs, "book": self.addressbook
        , "mods":self.mods }
        
        ########
        
        
    
    def run (self):
        for t in self.TYPE:
            if t == "S":
                server_thread = threading.Thread(target=self.startServer, args = ("S",))
                server_thread.start()
                processor_thread = threading.Thread(target= self.startProcessor)
                processor_thread.start()
            elif t == "s":
                server_thread = threading.Thread(target=self.startServer, args = ("s",))
                server_thread.start()
            elif t == "B":
                broadcast_thread = threading.Thread(target=self.startBroadcast, args=("B", str(self.PORT).encode()))
                broadcast_thread.start()
            elif t == "b":
                broadcast_thread = threading.Thread(target=self.startBroadcast, args=("b", [self.id ,self.HOST, self.PORT]))
                broadcast_thread.start()
                time.sleep(2)
                
            elif t == "I":
                insructor_thread = threading.Thread(target=self.startInstructor, args = (self.promptls, ))
                insructor_thread.start()
            elif t == "i":
                terminator_thread = threading.Thread(target=self.localcmd)
                terminator_thread.start()

            
    
    def localcmd(self): # MAKE THIS A DICT
        
        while not self.kill: 
            prompt = input("")
            if prompt in self.promptls:
                self.promptls[prompt]()
        return

    def killall(self):
        self.kill = True
    def about (self):
        print (self.HOST , self.PORT, self.id, self.TYPE)
    def jobs(self):
        print (f"number of jobs: {len(self.TASKls)}")
        for j in self.TASKls:
            print (j)
    def addressbook(self):
        print (self.book.address)
    def mods(self):
        print (self.prgls)

def main():
    t = Telephone()
    t.start()

if __name__ == "__main__":
    main()