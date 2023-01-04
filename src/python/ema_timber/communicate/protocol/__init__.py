import os
import sys
current = os.path.dirname(os.path.abspath(__file__))
parent = os.path.dirname(current)
sys.path.append(parent)

print (f"Importing modules:")

modls = {}

for name in os.listdir(current):
    p = os.path.join(current, name)
    if os.path.isdir(p) and p[-2:] != "__":
        sys.path.append(p)
        
        try:
            globals()[name] = __import__(name)
            loc = globals()[name]
            print (name)
            print (loc)
            try:
                func =  getattr(loc , name)
                modls[name]= func
                print(f"\t{name} - ok")
            except Exception as e:
                
                modls[name] = loc 
                print(f"\t{name} - NA")
                print ("\t  ",e)
        except Exception as e:
            print (e)
            print(f"\t{name} - failed")       
print (f"Import done\n")

def getmods():
    return modls 
