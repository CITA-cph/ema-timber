import socket
import time 

class Client():
    def __init__(self):
        pass

    def clientSetup(self,HOST,PORT):
        
        s = socket.socket(socket.AF_INET,socket.SOCK_STREAM)
        s.settimeout(5)
        try:
            s.connect((HOST,PORT))
            #print(f"++++ Communicating with {HOST} : {PORT} ++++")
            return s
        except:
            #print(f"++++ Could not communicate with {HOST} : {PORT} ++++")
            return False

    def clientOut(self, TARGET_IP, TARGET_PORT,message):
        s = self.clientSetup(TARGET_IP,TARGET_PORT)
        try:
            s.send(message)
            #print(message)
            data = s.recv(1024)
            print (f"--- {data.decode()} ---")
            s.close()
            return True
        except:
            print(f"Could not send data")
            return False

    def PING(self, S_HOST, S_PORT, ID, IP, PORT, show = True):
        s = self.clientSetup(S_HOST,S_PORT)
        if s:
            s.send(b"PING")
            s.recv(1024)
            s.send(ID.encode())
            s.recv(1024)
            s.send(IP.encode())
            s.recv(1024)
            s.send(str(PORT).encode())
            s.recv(1024)
            if show:
                print (f"PING OK")
            s.close()
            return True
        else:
            return False

    def sendByteStream(self,TARGET_IP, TARGET_PORT,bytestream, dst):
        s = self.clientSetup(TARGET_IP,TARGET_PORT)
        s.settimeout(None)
        if s:
            s.send(b"FILE")
            s.recv(1024)
            s.send(str(len(bytestream)).encode())
            s.recv(1024)
            s.send(bytestream)
            message  = s.recv(1024)
            print(message.decode())
            s.send(dst.encode())
            s.recv(1024)
            print("SEND OK")
            s.close()

    def MODS(self,S_HOST, S_PORT, show = False):
        print (f"attempting to get mods from {S_HOST} {S_PORT}")
        s = self.clientSetup(S_HOST,S_PORT)
        ls  = []
        if s:
            s.send(b"MODS")
            s.recv(1024)
            s.send(b"waiting for list")
            no = s.recv(1024).decode()
            s.send(f"waiting for {no} items".encode())
            for n in range(int(no)):
                data  = s.recv(1024).decode()
                ls.append(data)
                s.send("received".encode())
            if show:
                print (f"MOD OK")
            s.close()
            return True, ls
        else:
            return False, False