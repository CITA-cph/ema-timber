import json
from . import Client
from . import Package
import time
def get_address(target = ""):

    with open("yellowpages.json","r") as f:
        j_obj = json.load(f)
    
    if target == "":
        my_addr = list(j_obj.keys())[0]
        return(j_obj[my_addr])

    if target in j_obj:
        return(j_obj[target])
    else:
        print(f"{target} is not callable")
        return False

def getprgls():
    return prgls

#######################
def OUTPUT(args):
    for a in args:
        print(a)
    return False,False

def takeImg(args):

    print("\n<takeImg> START\n")
    return_addr = args[0]
    try:
        folder = args[1][0]
    except:
        timestr = time.strftime(("%y%m%d_%H%M%S"))
        folder = "00000"
    answer_addr = "02"
    R_HOST, R_PORT = get_address(return_addr)
    A_HOST, A_PORT = get_address(answer_addr)

    camera_ls = ["04"]
    camera_addrs = []

    for id in camera_ls:
        print(f"{id} :")

        ad = get_address(id)
        if ad:
            camera_addrs.append(ad)
            res = Client.PING( ad[0],ad[1],"02",A_HOST, A_PORT)
            if not res:
                print (f"Could not connect to {id}")
                message = Package.pack(TASK="OUTPUT", args=[f"Could not get a respond from {id}"])
                Client.clientOut(R_HOST, R_PORT, message )
                print("\n<takeImg> FAILED\n")
                return False,False
        else:
            print (f"Could not connect to {id}")
            message = Package.pack(TASK="OUTPUT", args=[f"{id} has not connected to Server"])
            Client.clientOut(R_HOST, R_PORT, message )
            print("\n<takeImg> FAILED\n")
            return False,False

    for ad in camera_addrs:
        message = Package.pack(TASK="takeImg", args=["02", folder])
        Client.clientOut(ad[0], ad[1], message)
        message = Package.pack(TASK="OUTPUT", args=[f"Cameras - {camera_ls}, has started taking photos"])
        Client.clientOut(R_HOST, R_PORT, message )

    print("\n<takeImg> DONE\n")
    return False,False
    


#######################

prgls = {

        "OUTPUT":OUTPUT,
        "takeImg":takeImg,
        
        }
