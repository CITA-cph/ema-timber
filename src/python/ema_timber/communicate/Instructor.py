import time

from . import Client
from . import Package

class Instructor():

    def __init__(self,T_HOST, T_PORT, id = 99, i_s = {}):
        print ("HERE")
        self.id = id
        self.prot = list(i_s.keys())
        self.HOST = T_HOST
        self.PORT = T_PORT
        self.instruction()

    def instruction(self):
        
        while True:
            time.sleep(1)
            print ("\n+++++++++++[ PROGRAMS ]+++++++++++")
            for id,prot in enumerate(self.prot):
                print (f"{[ id ]} ----- {[prot]}")
            chosen_prot = input('Func : \t')
            chosen_prot = int(chosen_prot)
            args = [self.id,]
            if chosen_prot < len(self.prot):
                extra_v = input('Args : \t')
                if len(extra_v)> 0:
                    extra_ls = extra_v.split(",")
                    args.append(extra_ls)
                
            else:
                print( f" {[chosen_prot]} not in list of programs")
                continue
            
            message  = Package.pack(TASK = self.prot[chosen_prot], args= args)
            s = Client.clientOut(self.HOST, self.PORT, message)



