# PROCESSOR
from queue import Queue
from threading import Thread

from . import Package
from . import Yellowpages

class Processor(Yellowpages.Yellowpages):

    def __init__(self,id, prgls,no_drones=1):
        Yellowpages.Yellowpages.__init__(self,id)
        self.id = id
        self.prgls = prgls
        self.jobqueue = Queue()
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
        print (f"{no_drones} thread(s)")
        for i in range(no_drones):
            worker = Thread(target=self.dostuff, args=(), daemon=True)
            worker.start()
    
    def dostuff(self):
        while True:
            try:
                fu, args = self.jobqueue.get()
                try:
                    task = fu(args , self.address)
                    try:
                        f0, args0 = task.out()
                    except:
                        continue
                    if f0 and f0 in self.prgls:
                        self.jobqueue.put([self.prgls[f0],args0])
                        

                except Exception as e:
                    print (e)
                    print (f"error with  {fu}")

            except self.jobqueue.Empty:
                continue

            else:
                self.jobqueue.task_done()

    
    