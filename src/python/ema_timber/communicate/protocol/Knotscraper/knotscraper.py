# HERE IS WHERE YOU PUT HOW THE FUNCTION INTERACTS WITH OTHER SYSTEMS

import os
import sys

current = os.path.dirname(os.path.abspath(__file__))
#print (current)
core = os.path.abspath(os.path.join(current ,".."))
#print(core)
sys.path.append(core)

import Wrapper
import time
from . import getData

#print ("DEEP KNOT")

class Knotscraper():

    def __init__(self, args , pages):
        # args = [ retrunaddr ,[task, arguments for task] ] 
        self.book =  pages
        self.id = list(self.book.keys())[0]
        self.re_addr = args[0]
        self.task = args[1]
        self.outputA = False
        self.outputB = False
        self.camls = ["10"]
        self.perform()


    def perform(self):
        task = self.task[0]
        
        match task:

            case "callCam": # INSTRUCTOR TO SERVER TO CAMERA
                self.callCam()
            case "takeImg": # CAMERA
                self.takeImg()
                pass
            case "sendImg": # CAMERA TO SERVER
                pass
            case "processImg": # SERVER
                pass
            case "done": # SERVER TO INSTRUCTOR
                pass
            case "listen": # USED TO PRINT 
                if len(self.re_addr) > 2:
                    print (self.task[1])
                    chain = self.re_addr[:-2]
                    T_HOST, T_PORT = self.book[chain[-2:]]
                    message = Wrapper.Package.pack(TASK="Knotscraper", args = [chain,["listen", self.task[1]]])
                    Wrapper.Client.clientOut(T_HOST, T_PORT, message)
                else:
                    print (self.task[1])
                self.outputA = False
                self.outputB = False
            
            case _:

                T_HOST, T_PORT = self.book[self.re_addr[-2:]]
                message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.re_addr[-2:],["listen",f"{self.id} - Command < {task} > not recognized"]])
                Wrapper.Client.clientOut(T_HOST, T_PORT, message)
                print (f"Command < {task} > not recognized")
                self.outputA = False
                self.outputB = False

    def callCam(self):

        chain = self.re_addr + self.id

        I_HOST, I_PORT = self.book[self.re_addr]
        S_HOST, S_PORT = self.book[self.id]

        for c in self.camls:

            if c in self.book:
                c_HOST, c_PORT = self.book[c]
                print (f"{c} :")
                res = Wrapper.Client.PING(c_HOST, c_PORT, self.id, S_HOST, S_PORT )
                
                if res:
                    
                    message = Wrapper.Package.pack (
                        TASK= "Knotscraper", 
                        args = [chain, ["takeImg", "rawImg"]]
                    )
                    Wrapper.Client.clientOut(c_HOST, c_PORT, message)

                    message = Wrapper.Package.pack (
                        TASK= "Knotscraper", 
                        args = [self.id, ["listen", f"Camera {c} has started taking photos"]]
                    )

                    self.outputA = False
                    self.outputB = False
                    
                    
                
                else:
                    print (f"Could not connect to {c}")
                    message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.id,["listen", f"Could not get a respond from {c}"]])
                    Wrapper.Client.clientOut(I_HOST, I_PORT, message)
                    print (f"<Knotscraper> -FAILED - Could not get a respond from {c}")
                    self.outputA = False
                    self.outputB = False

            else:
                print (f"{c} has not connected to the Server")
                message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.id,["listen", f"{c} has not connected to the Server"]])
                Wrapper.Client.clientOut(I_HOST, I_PORT, message)
                print (f"<Knotscraper> -FAILED - {c} has not connected to the Server")
                self.outputA = False
                self.outputB = False
    
    def takeImg(self):

        S_HOST, S_PORT = self.book[self.re_addr[-2:]]
        filename = self.task[1]

        try:

            from picamera2 import Picamera2
            picam2 = Picamera2()
            config = picam2.create_still_configuration(main = {"size": picam2.sensor_resolution})
            picam2.configure(config)

            picam2.start()
            time.sleep(2)
            picam2.stop()
            
            # TAKE IMAGE CODE HERE#
            for i in range(10):
                picam2.start()
                time.sleep(1)
                raw = picam2.capture_array("main")
                raw_np = np.array(raw).tobytes()
                print (len(raw_np))
                print (raw.shape)
                Wrapper.Client.sendByteStream(S_HOST, S_PORT, raw_np, f"np_array/{filename}/{i:03}")
                picam2.stop()
            
            picam2.close()
            print ("takeImg - ok | sendImg - ok")

            message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.re_addr,["listen", f"{self.id} - takeImg - ok | sendImg - ok"]])
            Wrapper.Client.clientOut(S_HOST, S_PORT, message)

            date =  time.strftime("%y%m%d")
            message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.re_addr,["processImg", date]])
            Wrapper.Client.clientOut(S_HOST, S_PORT, message)
            
            print("<takeImg> DONE")
            self.outputA = False
            self.outputB = False

        except Exception as e:
            message = Wrapper.Package.pack(TASK="Knotscraper", args = [self.re_addr,["listen", f"{self.id} - {e}"]])
            Wrapper.Client.clientOut(S_HOST, S_PORT, message)
            print (e)
            self.outputA = False
            self.outputB = False

    def out (self): # OUPUT OF CLASS
        return self.outputA, self.outputB
