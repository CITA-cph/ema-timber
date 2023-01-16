from queue import Queue
from threading import Thread

from . import Package

class Processor():
    def __init__(self, TASKls = [], prgls = {} , book = None ):
        self.TASKls = TASKls
        self.jobqueue = Queue()
        self.prgls = prgls
        self.dronels = []
        self.kill = False
        self.no_drones = 1
        self.book = book

    def postJob(self, job):
        message  = Package.unpack(job)
        f, args = message["TASK"], message["args"] #PACKAGE UNPACK HERE
        if f in self.prgls:
            self.jobqueue.put([self.prgls[f], args])
        else:
            print(f"{f} not found ")
            self.jobqueue.put() #PACKAGE PACK AND RETURN FUNCTION NOT FOUND
    
    def startProcessor(self):
        self.drone(self.no_drones)
        while not self.kill:
            for t in self.TASKls:
                self.postJob(t)
                self.TASKls.remove(t)
        
        for no_thread, i in enumerate (self.dronels):
            i.join()
            print (f"Thread {no_thread+1}/{len(self.dronels)} - end")
        return
    def drone(self, no_drones):   
        print (f"{no_drones} thread(s)")
        for i in range(no_drones):
            worker = Thread(target=self.dostuff, args=(), daemon=True)
            worker.start()
            self.dronels.append(worker)

    def dostuff(self):
        while not self.kill:
            try:
                fu, args = self.jobqueue.get(timeout= 2)
                try:
                    task = fu(args , self.book.address)
                    try:
                        f0, args0 = task.out() # Could be use to trigger another event. If not used just return FALSE FALSE
                    except:
                        continue
                    if f0 and f0 in self.prgls:
                        self.jobqueue.put([self.prgls[f0],args0])
                    else:
                        self.jobqueue.task_done()
                except Exception as e:
                    print (e)
                    print (f"error with  {fu}")

            except:
                continue
        return