import time
from . import Client
from . import Package


class Instructor():

    def __init__(self,T_HOST, T_PORT, id = 99,):
        self.id = id
        self.HOST = T_HOST
        self.PORT = T_PORT
        r , self.prot = Client.MODS(T_HOST,T_PORT)
        if not r :
            print ("Failed to get mod list")
        self.instruction()

    def instruction(self):
        
        while True:
            time.sleep(0.2)
            print ("\n+++++++++++[ PROGRAMS ]+++++++++++")
            for id,prot in enumerate(self.prot):
                print (f"{[ id ]} ----- {[prot]}")
            chosen_prot = input('Func : \t')
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
            s = Client.clientOut(self.HOST, self.PORT, message)

if __name__ == "__main__":

    import socket
    HOSTNAME = socket.gethostname()
    HOST = socket.gethostbyname(HOSTNAME)
    i = Instructor(HOST, 55556, "99")
    