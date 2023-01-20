# HERE IS WHERE YOU PUT HOW THE FUNCTION INTERACTS WITH OTHER SYSTEMS

from ema_timber.communicate.protocol import Wrapper
import time
import math
import numpy as np
try:
    from picamera2 import Picamera2
    from .Easydriver import easydriver as ed
except:
    pass
#print ("DEEP KNOT")

class Knotscraper():

    def __init__(self, args , pages):
        # args = [ retrunaddr ,[task, arguments for task] ] 
        self.push = Wrapper.Client()
        self.book =  pages
        self.id = list(self.book.keys())[0]
        self.re_addr = args[0]
        self.task = args[1]
        self.outputA = False
        self.outputB = False
        self.camls = ["10",]
        self.perform()
        


    def perform(self):
        task = self.task[0]
        
        programs = {

            "callCam" : self.callCam,
            "takeImg" : self.takeImg,
            "processImg": self.processImg,
            "listen": self.listen,

        }

        if task in programs:
            programs[task]()

        else:
            T_HOST, T_PORT = self.book[self.re_addr[-2:]]
            message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.re_addr[-2:],["listen",f"{self.id} - Command < {task} > not recognized"]])
            self.push.clientOut(T_HOST, T_PORT, message)
            print (f"Command < {task} > not recognized")
            self.outputA = False
            self.outputB = False

    def callCam(self):

        chain = self.re_addr + self.id

        I_HOST, I_PORT = self.book[self.re_addr]
        S_HOST, S_PORT = self.book[self.id]

        filename = self.task[1]
        totallen = float(self.task[2])

        for c in self.camls:

            if c in self.book:
                c_HOST, c_PORT = self.book[c]
                print (f"{c} :")
                res = self.push.PING(c_HOST, c_PORT, self.id, S_HOST, S_PORT )
                
                if res:
                    
                    message = Wrapper.Package.pack (
                        TASK= "Knotscraper", 
                        args = [chain, ["takeImg", filename, str(totallen)]]
                    )
                    self.push.clientOut(c_HOST, c_PORT, message)

                    message = Wrapper.Package.pack (
                        TASK= "Knotscraper", 
                        args = [self.id, ["listen", f"Camera {c} has started taking photos"]]
                    )
                    self.push.clientOut(I_HOST, I_PORT, message)

                else:
                    print (f"Could not connect to {c}")
                    message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.id,["listen", f"Could not get a respond from {c}"]])
                    self.push.clientOut(I_HOST, I_PORT, message)
                    print (f"<Knotscraper> -FAILED - Could not get a respond from {c}")
            else:
                print (f"{c} has not connected to the Server")
                message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.id,["listen", f"{c} has not connected to the Server"]])
                self.push.clientOut(I_HOST, I_PORT, message)
                print (f"<Knotscraper> -FAILED - {c} has not connected to the Server")
            self.outputA = False
            self.outputB = False
    
    def takeImg(self):

        
        S_HOST, S_PORT = self.book[self.re_addr[-2:]]
        filename = self.task[1]
        totalLen = float(self.task[2])
        intv = 100 
        no_frames = math.ceil(totalLen/intv)
        current_loc = 0

        try:
            stepper = ed(0.00001,18, 23,24,17,25,27)
            stepper.enable()
            stepper.set_direction(True) # cw = T ccw = F


            picam2 = Picamera2()
            config = picam2.create_still_configuration(main = {"size": picam2.sensor_resolution})
            picam2.configure(config)

            picam2.start()
            time.sleep(2) # Warmup camera
            
            # TAKE IMAGE CODE HERE#
            for i in range(no_frames):
                print (f"Taking {filename} : {i:03}")
                stepper.step(intv)
                time.sleep(1)
                raw = picam2.capture_array("main")
                raw_np = np.array(raw).tobytes()
                print (len(raw_np))
                print (raw.shape)
                self.push.sendByteStream(S_HOST, S_PORT, raw_np, f"np_array/{filename}/{i:03}")
                current_loc += intv
                print (f"moving to {current_loc}")
            
            picam2.close()
            print ("takeImg - ok | sendImg - ok")

            message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.re_addr,["listen", f"{self.id} - takeImg - ok | sendImg - ok"]])
            self.push.clientOut(S_HOST, S_PORT, message)

            date =  time.strftime("%y%m%d")
            message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.re_addr,["processImg", date , filename ]])
            self.push.clientOut(S_HOST, S_PORT, message)
            
            print ("reseting camera")
            stepper.set_direction(False)
            stepper.step(intv*no_frames)
            stepper.disable()
            stepper.finish()
            
            print("<takeImg> DONE")

        except Exception as e:
            message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.re_addr,["listen", f"{self.id} - {e}"]])
            self.push.clientOut(S_HOST, S_PORT, message)
            print (e)

        self.outputA = False
        self.outputB = False

    def processImg(self):
        
        import os
        from . import getData
        date = self.task[1]
        filename = self.task[2]
        base_dir = os.path.abspath(f"./examples/python/")
        T_HOST, T_PORT = self.book[self.re_addr[:-2]]

        try:
            print ("processing images...")
            getData.run (base_dir, "data/"+date, filename)
            print ("processImg - ok")
            message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.re_addr,["listen", f"<processImg> DONE"]])

        except Exception as e:
            message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.re_addr,["listen", f"{self.id} - {e}"]])
            print (e)


        self.push.clientOut(T_HOST, T_PORT, message)
        self.outputA = False
        self.outputB = False
        return 
        
    def listen(self):
        if len(self.re_addr) > 2:
            print (self.task[1])
            chain = self.re_addr[:-2]
            T_HOST, T_PORT = self.book[chain[-2:]]
            message = Wrapper.Package.pack(TASK="Knotscraper", args = [chain,["listen", self.task[1]]])
            self.push.clientOut(T_HOST, T_PORT, message)
        else:
            print (self.task[1])
        self.outputA = False
        self.outputB = False

    def out (self): # OUPUT OF CLASS
        return self.outputA, self.outputB
