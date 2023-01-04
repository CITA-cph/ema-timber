# HERE IS WHERE YOU PUT HOW THE FUNCTION INTERACTS WITH OTHER SYSTEMS

import os
import sys

current = os.path.dirname(os.path.abspath(__file__))
#print (current)
core = os.path.abspath(os.path.join(current ,".."))
#print(core)
sys.path.append(core)

import Wrapper
import talk

print ("DEEP HELLO")
class Helloworld():

    def __init__(self, args , pages ): # PERFORM TASK
        self.book =  pages
        self.id = list(self.book.keys())[0]
        self.re_addr = args[0]
        self.outputA = False
        self.outputB = False
        if len(args)> 1:
            self.re_othes = args[1]
            self.hello()
        else:
            talk.wave_at(self.re_addr)

    def hello (self): # TASK

        for a in self.re_othes:
            if a in self.book:
                T_HOST, T_PORT = self.book[a]
                message = Wrapper.Package.pack("Helloworld", [self.re_addr])
                Wrapper.Client.clientOut(T_HOST, T_PORT, message)
            else:
                T_HOST, T_PORT = self.book[self.re_addr]
                message = Wrapper.Package.pack("Helloworld", ["the void"])
                Wrapper.Client.clientOut(T_HOST, T_PORT, message)
                self.outputA = "Helloworld"
                self.outputB = ["the void"]
            talk.observe(self.re_addr, a)

    def out (self): # OUPUT OF CLASS
        return self.outputA, self.outputB

if __name__ == "__main__":
    h = Helloworld(["00", ["01", "02", "03"]])