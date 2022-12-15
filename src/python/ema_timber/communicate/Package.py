# MESSAGE
import json

def pack(TASK="", args =[]):
    return json.dumps({"TASK": TASK, "args":args}).encode("utf-8")

def unpack(message):
    x = json.loads(message.decode("utf-8"))
    return x
