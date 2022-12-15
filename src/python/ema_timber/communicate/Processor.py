# PROCESSOR
from queue import Queue
from threading import Thread

from . import Package
from . import Yellowpages

class Processor():

    def __init__(self, prgls,no_drones=1):
        
        self.prgls = prgls
        self.jobqueue = Queue()
        self.pages = Yellowpages.Yellowpages()
        self.drones = self.drone(no_drones)
    
    def postJob(self, job):
        message  = Package.unpack(job)
        f, args = message["TASK"], message["args"] #PACKAGE UNPACK HERE
        if f in self.prgls:
            self.jobqueue.put([self.prgls[f], args])
        else:
            print(f"{f} not found ")
            self.jobqueue.put() #PACKAGE PACK AND RETURN FUNCTION NOT FOUND

    def drone(self, no_drones):   
        
        for i in range(no_drones):
            worker = Thread(target=self.dostuff, args=(), daemon=True)
            worker.start()
    
    def dostuff(self):
        while True:
            try:
                f, args = self.jobqueue.get()
                f0, args0 = f(args)
                if f0:
                    self.jobqueue.put([f0,args0])
            except self.jobqueue.Empty:
                continue
            else:
                self.jobqueue.task_done()
    
    