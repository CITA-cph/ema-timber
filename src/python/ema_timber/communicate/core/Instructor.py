import time
from .Client import Client
from . import Package



class Instructor():

    def __init__(self, id = "909"):
        self.id = id
        self.kill = False
        self.T_HOST = None
        self.T_PORT  = None
        self.out = Client()

    def setupInstructor(self):
        r , self.prot = self.out.MODS(self.T_HOST,self.T_PORT)
        if  not r :
            print ("Failed to get mod list")
            print ("Try again when server is running")
            self.prot = []
            self.kill = True
            return False
        return True

    def startInstructor(self,localcmd):
        self.setupInstructor()
        self.showmods()
        while not self.kill:
            time.sleep(0.2)
            chosen_prot = input("")
            if chosen_prot in localcmd:
                localcmd[chosen_prot]()
                continue
            elif chosen_prot == "refresh":
                self.setupInstructor()
                continue
            elif chosen_prot == "show":
                self.showmods()
                continue
            try:
                chosen_prot = int(chosen_prot)
            except:
                print( f"Use index")
                continue

            args = [self.id,]
            if chosen_prot < len(self.prot):
                extra_v = input('Args : \t')
                if len(extra_v)> 0:
                    extra_ls = extra_v.split("/")
                    args.append(extra_ls)
                
            else:
                print( f"{[chosen_prot]} not in list of programs")
                continue
            
            message  = Package.pack(TASK = self.prot[chosen_prot], args= args)
            s = self.out.clientOut(self.T_HOST, self.T_PORT, message)
        print ("instructor - end")

    def showmods(self):
        print ("\n+++++++++++[ PROGRAMS ]+++++++++++")
        for id,prot in enumerate(self.prot):
                print (f"{[ id ]} ----- {[prot]}")
