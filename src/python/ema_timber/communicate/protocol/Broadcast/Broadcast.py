# BROADCASTING CHANNEL
import socket
import time

class Broadcast():

    def __init__(self, HOST= "127.0.0.1", PORT= 55555, MESSAGE=b"WOOD IS COOL"):
        
        self.HOST = HOST
        self.PORT = PORT
        self.MESSAGE = MESSAGE

        self.broadcasting()

    def broadcasting(self):

        server = socket.socket(socket.AF_INET, socket.SOCK_DGRAM, socket.IPPROTO_UDP)
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
        server.settimeout(0.2)
        print(f"Broadcasting [ {self.HOST} : {self.PORT} ] : {self.MESSAGE.decode()}")

        while True:
            server.sendto(self.MESSAGE, ("<broadcast>", self.PORT))
            time.sleep(1)

class Audience():

    def __init__(self, PORT= 55555):

        self.addr , self.message = self.tunein(PORT)

    def tunein(self,PORT):

        client = socket.socket(socket.AF_INET, socket.SOCK_DGRAM, socket.IPPROTO_UDP)  # UDP
        client.settimeout(2)
        client.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        client.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
        try:
            client.bind(("", PORT))
            data, addr = client.recvfrom(1024)
            try:
                return addr[0], int(data.decode())
            except: 
                return addr[0], data.decode()
        except Exception as e:
            #print (e)
            #print (f"Failed to reach broadcast at {PORT}")
            return False, False

    def __enter__(self):
        return self
    def __exit__(self, exc_type, exc_val, exc_tb):
        del self.addr
        del self.message

if __name__ == "__main__":
    HOSTNAME = socket.gethostname()
    HOST = socket.gethostbyname(HOSTNAME)
    Broadcast(HOST)