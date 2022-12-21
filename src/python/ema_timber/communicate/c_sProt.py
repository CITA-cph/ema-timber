import json
import time
from . import Client
from . import Package
import numpy as np
import os 

def get_address(target = ""):

    with open("yellowpages.json","r") as f:
        j_obj = json.load(f)
    
    if target == "":
        my_id = list(j_obj.keys())[0]
        return(my_id)

    if target in j_obj:
        return(j_obj[target])
    else:
        print(f"{target} is not callable")
        return False

def getprgls():
    return prgls

def OUTPUT(args):
    for a in args:
        print(a)
    return False,False

# ALL FUNC SHOULD BE IN A SEPERATE FILE
def takeImg(args):

    try:
        from picamera2 import Picamera2

        print("<takeImg> START")

        return_addr = args[0]
        filename  = args[1]
        R_HOST, R_PORT = get_address(return_addr)
        
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
            Client.sendByteStream(R_HOST, R_PORT, raw_np, f"np_array/{filename}/{i:03}")
            picam2.stop()

        picam2.close()

        message = Package.pack("OUTPUT", [ "<takeImg> DONE"])
        Client.clientOut(R_HOST, R_PORT,message)
        date =  time.strftime("%y%m%d")
        message = Package.pack("stitchImg", [date])
        Client.clientOut(R_HOST, R_PORT,message)
        print("<takeImg> DONE")
    except:
        print("<takeImg> FAILED")

    return False, False

def stitchImg(args):

    filename  = args[0]
    from .. import process
    from process.knotscrapper import knotscrap as ks
    base_dir = os.path.abspath(f"../ema-timber/examples/{filename}")
    ks.run(base_dir)

    


prgls = {
        "OUTPUT":OUTPUT,
        "takeImg":takeImg,
        "stitchImg":stitchImg,
        }
