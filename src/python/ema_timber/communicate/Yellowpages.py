# YELLOW PAGES
import json
# {id:[ip, port]}

class Yellowpages:

    def __init__(self,dir = {}):
        self.address = dir
        self.dest = "yellowpages.json"
        self.updateJson()
        
    def set_address(self, dir = {}):
        for a in dir.keys(): 
            self.address[a] = dir[a]
        self.updateJson()
        
    def get_address(self, target):
        with open(self.dest,"r") as f:
            j_obj = json.load(f)
        if target in j_obj:
            return(j_obj[target])
        else:
            print(f"{target} is not callable")
            return False

    def updateJson(self):
        j_obj = json.dumps(self.address, indent=4)
        with open(self.dest,"w") as f:
            f.write(j_obj)