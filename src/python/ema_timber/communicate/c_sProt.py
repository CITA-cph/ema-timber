import json
import time
from . import Client
from . import Package

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


def takeImg(args):

    print("<takeImg> START")

    return_addr = args[0]
    R_HOST, R_PORT = get_address(return_addr)
    A_ID = get_address()
    # TAKE IMAGE CODE HERE#
    for i in range(10):
        print ("TAKING IMAGE ", i)
        time.sleep(0.5)
        message = Package.pack("OUTPUT", [ f"IMAGE{i}"])
        Client.clientOut(R_HOST, R_PORT,message)

    message = Package.pack("OUTPUT", [ "<takeImg> DONE"])
    Client.clientOut(R_HOST, R_PORT,message)
    print("<takeImg> DONE")
    return False, False
    



prgls = {
        "OUTPUT":OUTPUT,
        "takeImg":takeImg,
        }
