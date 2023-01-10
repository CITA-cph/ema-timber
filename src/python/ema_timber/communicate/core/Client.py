# CLIENT CHANNEL
# THIS MODULE SEND INFORMATION TO THE SERVER

# clientSetup - creates a client server
# clientOut - sends data to target and closes connection (links to Server.updatePages)
# PING - tries to send local addr to target
# sendByteStream - sends a string of bytes (links to Server.recvBytestream)
# MODS - asks for mod list (links to Server.send_modls)

# FEATURES TO ADD
#   1. make this a class
#   2. ALIVE should terminate
#   3. Client class should inherit threading


import socket
import time 

def clientSetup(HOST,PORT):
    
    s = socket.socket(socket.AF_INET,socket.SOCK_STREAM)
    s.settimeout(5)
    try:
        s.connect((HOST,PORT))
        #print(f"++++ Communicating with {HOST} : {PORT} ++++")
        return s
    except:
        #print(f"++++ Could not communicate with {HOST} : {PORT} ++++")
        return False

def clientOut(TARGET_IP, TARGET_PORT,message):
    s = clientSetup(TARGET_IP,TARGET_PORT)
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

def PING(S_HOST, S_PORT, ID, IP, PORT, show = True):
    s = clientSetup(S_HOST,S_PORT)
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

def sendByteStream(TARGET_IP, TARGET_PORT,bytestream, dst):
    s = clientSetup(TARGET_IP,TARGET_PORT)
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

def MODS(S_HOST, S_PORT, show = False):
    s = clientSetup(S_HOST,S_PORT)
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

def ALIVE(S_HOST, S_PORT, ID, IP,  PORT):
    delta  = 0
    while True:
        res = PING(S_HOST, S_PORT, ID, IP, PORT, False)
        if res:
            
            if delta > 0:
                print  ("\nReconnected to server")
            delta = 0
            time.sleep(5)
        else:
            if delta == 0:
                print ("\nLost connection to server")
            time.sleep(2)
            delta += 1