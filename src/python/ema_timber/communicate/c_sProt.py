import json
import time
from . import Client
from . import Package
import numpy as np

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
        from picamera2 import Picamera2, Preview

        print("<takeImg> START")

        return_addr = args[0]
        R_HOST, R_PORT = get_address(return_addr)
        A_ID = get_address()
        
        picam2 = Picamera2()
        picam2.start_preview(Preview.QTGL)

        preview_config = picam2.create_preview_comfiguration(raw = {"size": picam2.sensor_resolution})
        picam2.configure(preview_config)

        picam2.start()

        # TAKE IMAGE CODE HERE#
        for i in range(10):
            time.sleep(0.5)
            raw = picam2.capture_array("raw")
            raw_np = np.array(raw).tobytes()
            Client.sendByteStream(R_HOST, R_PORT, raw_np)

        message = Package.pack("OUTPUT", [ "<takeImg> DONE"])
        Client.clientOut(R_HOST, R_PORT,message)
        print("<takeImg> DONE")
    except:
        print("<takeImg> FAILED")

    return False, False




prgls = {
        "OUTPUT":OUTPUT,
        "takeImg":takeImg,
        }
