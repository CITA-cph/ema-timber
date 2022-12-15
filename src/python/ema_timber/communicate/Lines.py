# QUEUE METHODS
from queue import Queue
from threading import Thread

class Lines():
    
    def __init__(self, no_cons = 1):
        
        self.inqueue = Queue()
        self.outqueue = Queue()
        self.drones = self.drone(no_cons)

    def dostuff(self):

        while True:
            try:
                item = self.inqueue.get()
                self.outqueue.put(item[0](item[1]))
            except self.inqueue.Empty:
                continue
            else:
                self.inqueue.task_done()

    def drone(self, no):

        for i in range(no):
            worker = Thread(target=self.dostuff, args=(), daemon=True)
            worker.start()
        

    def addjob(self, job):
        self.inqueue.put(job)

if __name__ == "__main__":

    l = Lines()

    for i in range(5):
        l.addjob([print, i])
        for j in range(5):
            l.addjob([print, j*i])
    
    l.inqueue.join()
    print("ALL WORK DONE")
    print (l.outqueue.qsize())
    print (l.outqueue.get())