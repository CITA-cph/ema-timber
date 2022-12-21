# CLIENT CHANNEL

import socket
import time 

def clientSetup(HOST,PORT):
    
    s = socket.socket(socket.AF_INET,socket.SOCK_STREAM)

    try:
        s.connect((HOST,PORT))
        print(f"++++ Communicating with {HOST} : {PORT} ++++")
        return s
    except:
        print(f"++++ Could not communicate with {HOST} : {PORT} ++++")
        return False

def clientOut(TARGET_IP, TARGET_PORT,message):
    s = clientSetup(TARGET_IP,TARGET_PORT)
    try:
        s.send(message)
        print(message)
        data = s.recv(1024)
        print (f"--- {data.decode()} ---")
        s.close()
        return True
    except:
        print(f"Could not send data")
        return False

def PING(S_HOST, S_PORT, ID, IP, PORT):
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
        print (f"PING OK")
        s.close()
        return True
    else:
        return False

def sendByteStream(TARGET_IP, TARGET_PORT,bytestream, dst):
    s = clientSetup(TARGET_IP,TARGET_PORT)
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

