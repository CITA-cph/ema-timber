import cv2
import numpy as np
import prepFile

class Board:
    def __init__(self, id,  imgs, status):

        self.id = id
        self.imgs = [imgs] #[raw, stitched, knots, looked ]
        self.status = status
        self.bound = [] #[ contours, contour pos ]
        self.knots = [] #[ keypoints, knots pos, knots rect]

    def contrastImg(self, clipLimit = 2.0, tileGridSize=(25,5)):
        
        lab= cv2.cvtColor(self.imgs[1], cv2.COLOR_BGR2LAB)
        l_channel, a, b = cv2.split(lab)
        clahe = cv2.createCLAHE(clipLimit, tileGridSize)
        cl = clahe.apply(l_channel)
        limg = cv2.merge((cl,a,b))
        img = cv2.cvtColor(limg, cv2.COLOR_LAB2BGR)
        self.imgs[1] = img

    def getContoursPos(self):
        c = self.bound[0]
        v_ls = []
        for n in range(len(c)):
            v_ls.append((c[n][0][0],-1*c[n][0][1]))
        
        self.bound.append(v_ls)

    def getContours(self,lower = 50, upper = 150):

        img = self.imgs[1]
        img2 = img.copy()
        imggray = cv2.cvtColor(img2, cv2.COLOR_BGR2GRAY)
        ret, thresh = cv2.threshold(imggray, lower, upper, 0)
        contours, hierarchy = cv2.findContours(thresh, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE)

        if len(contours) != 0:
            # find the biggest countour (c) by the area
            sorted_contours= sorted(contours, key=cv2.contourArea, reverse= True)
        
        self.bound.append(sorted_contours[0])
        self.getContoursPos()

    def getKeypointPos(self):
        kp = self.knots[0]
        pos_ls = []
        for k in kp:
            x,y = k.pt
            s = int(k.size)
            x = int(x)
            y = -1*int(y)
            pos_ls.append((x,y,s))
        self.knots.append(pos_ls)

    def getKeyBlobs(self, filterByArea = False,filterByConvexity = False,filterByInertia = False,minArea =100,minConvexity = 0.1,minInertiaRatio = 0.1):
    
        img = self.imgs[1]
        gray_img = cv2.cvtColor(img , cv2.COLOR_BGR2GRAY)
        Gaussian = cv2.GaussianBlur(gray_img, (45,15), 0) # CHANGE THIS VALUE IF NEEDED
        params = cv2.SimpleBlobDetector_Params()
        params.filterByArea = filterByArea 
        params.filterByConvexity = filterByConvexity
        params.filterByInertia = filterByInertia
        params.minArea = minArea 
        params.minConvexity = minConvexity
        params.minInertiaRatio = minInertiaRatio
        detector = cv2.SimpleBlobDetector_create(params)
        keypoints = detector.detect(Gaussian)
        self.knots.append(keypoints)
        self.getKeypointPos()
     
    def scan(self, show = True,  bounds = False, knots = False, resize = False , s = 0.5):
        
        img = self.imgs[1].copy()
        blank = np.zeros((img.shape[0],img.shape[1],img.shape[2]), np.uint8)
        if bounds:
            contour_img = cv2.drawContours(blank, self.bound[0], -1,  (0,255,0), 1)
            img= cv2.addWeighted(img,0.8,contour_img,0.8,1)
        if knots:
            img = cv2.drawKeypoints(img, self.knots[0], np.array([]), (0,0,255), cv2.DRAW_MATCHES_FLAGS_DRAW_RICH_KEYPOINTS)
            
            for id  in range(len(self.knots[2])):
                r = self.knots[2][id]
                cv2.rectangle(img,r[0], r[1], (255,0,0),1)

                t = self.knots[1][id]
                id = prepFile.naming(id)
                txt = "id:{} x:{} y:{} s:{}".format(id, t[0], -1*t[1],t[2])
                img = cv2.putText(img,txt,(t[0], -1*t[1]),cv2.FONT_HERSHEY_SIMPLEX,0.3,(0,0,255),1,cv2.LINE_AA)
        self.imgs.append(img)

        if resize:
            img = cv2.resize(img, (0, 0), fx = s, fy = s)
 
        if show:
            cv2.imshow(self.id, img)
            cv2.waitKey(0)
            cv2.destroyAllWindows()

    def cropKnots(self):

        knots_img = []
        knots_rect = []
        img = self.imgs[1].copy()
        ih, iw, c = img.shape

        for i in range(len(self.knots[0])):
            
            k  = self.knots[0][i]
            h = int(k.size*1.15)
            w = h
            x,y = k.pt
            x = int(x)
            y = int(y)
        
            if x-h <0:
                up_l_X = 0
            else:
                up_l_X = x-h
            
            if y-w < 0:
                up_l_Y = 0
            else:
                up_l_Y= y-w

            if x+h > iw:
                bot_r_X = iw
            else:
                bot_r_X = x+h
            
            if y+w > ih:
                bot_r_Y = ih
            else:
                bot_r_Y= y+w
            
            up_l = (up_l_X, up_l_Y)
            bot_r = (bot_r_X , bot_r_Y)
            rect = (up_l,bot_r)
            knots_rect.append(rect)
            
            cropimg = img[up_l_Y:bot_r_Y ,up_l_X:bot_r_X]
            knots_img.append(cropimg)

        self.imgs.append(knots_img)
        self.knots.append(knots_rect)