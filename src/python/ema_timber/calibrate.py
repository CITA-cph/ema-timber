import cv2
import numpy as np
import math

corners = []
instructions = ["Top left", "Top right", "Bottom right", "Bottom left"]
mouseX = 0
mouseY = 0

def optimize_corner(img, c, ws=50):
    '''
    img : original image
    c : corner
    ws : window size for Harris filter
    '''
    hs = ws // 2
    minx = max(0, c[0] - ws)
    miny = max(0, c[1] - ws)
    crop = img[miny:miny + ws + ws, minx:minx + ws + ws]

    gray = cv2.cvtColor(crop,cv2.COLOR_BGR2GRAY)
    gray = np.float32(gray)
    dst = cv2.cornerHarris(gray,hs,3,0.04)
    ret, dst = cv2.threshold(dst,0.1*dst.max(),255,0)
    dst = np.uint8(dst)
    ret, labels, stats, centroids = cv2.connectedComponentsWithStats(dst)
    centroids = centroids[1:]

    criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 100, 0.001)
    subcorners = cv2.cornerSubPix(gray,np.float32(centroids),(5,5),(-1,-1),criteria)

    return (c[0] - ws + subcorners[0][0], c[1] - ws + subcorners[0][1])

def pick_point(event,x,y,flags,param):
    global corners, mouseX, mouseY

    mouseX = x
    mouseY = y

    if event == cv2.EVENT_LBUTTONDOWN:
        corners.append([x, y])

def get_perspective_correction(img_path, edge_length = 20, num_squares = 5, pixels_per_mm = 5):
    print("Starting...")
    global corners, instructions

    # Read image
    img = cv2.imread(img_path)
    print(f"Image shape: {img.shape}")
    scale = 960 / img.shape[0]
    img = cv2.resize(img, (int(img.shape[1] * scale), int(img.shape[0] * scale)))
    print(f"Resized image shape: {img.shape}")

    edgeLength = 22.2333
    pixels_per_mm = 5

    edge_length = edge_length * pixels_per_mm

    # Create calibration points
    numSquares = 5
    quadPoints = np.float32([
        [0,0],  
        [num_squares * edge_length, 0], 
        [num_squares * edge_length, num_squares * edge_length], 
        [0,num_squares * edge_length]])


    cv2.namedWindow('image')
    cv2.setMouseCallback('image', pick_point)

    calibrating = True
    while(calibrating):

        dispImg = img.copy()
        ch = 10

        # Draw existing polygon
        for i in range(len(corners) - 1):
            cv2.line(dispImg, corners[i], corners[i+1], (255,255,0), 1)

        # Draw live edge
        if len(corners) > 0:
            cv2.line(dispImg, corners[-1], (mouseX, mouseY), (255,255,0), 1)

        # Close polygon and end calibration
        if len(corners) >= 4:
            cv2.line(dispImg, corners[-1], corners[0], (255,255,0), 1)
            calibrating = False

        # Draw corners
        for c in corners:
            cv2.line(dispImg, (c[0] - ch, c[1]), (c[0] + ch, c[1]), (0,0,255), 1)
            cv2.line(dispImg, (c[0], c[1] - ch), (c[0], c[1] + ch), (0,0,255), 1)

        cv2.putText(dispImg, f"Num. squares: {num_squares}", (20, 30), cv2.FONT_HERSHEY_PLAIN, 1, (255,255,255), 1)
        if len(corners) < 4:
            cv2.putText(dispImg, instructions[len(corners)], (20, 50), cv2.FONT_HERSHEY_PLAIN, 1, (255,255,255), 1)

        # Show results
        cv2.imshow('image', dispImg)
        k = cv2.waitKey(1)# & 0xFF
        if k == 27:
            break

    if len(corners) != 4:
        print("Insufficient corners specified. Exiting...")
        return

    print("\nSet corners:")
    print(corners)

    # Optimize corners
    print("\nOptimizing corners...")
    optcorners = []

    for c in corners:
        optcorners.append(optimize_corner(img, c, 50))

    corners = np.float32(optcorners)
    print("\nOptimized corners:")
    print(corners)

    # Draw corners
    for c in corners:
        cv2.line(dispImg, (int(c[0]) - ch, int(c[1])), (int(c[0]) + ch, int(c[1])), (0,255,255), 1)
        cv2.line(dispImg, (int(c[0]), int(c[1]) - ch), (int(c[0]), int(c[1]) + ch), (0,255,255), 1)

    cv2.imshow('image', dispImg)
    cv2.waitKey(0)

    perspMat = cv2.getPerspectiveTransform(corners, quadPoints)

    coords = [
        [0,0], 
        [img.shape[1],0], 
        [img.shape[1], img.shape[0]], 
        [0, img.shape[0]]
        ]
    print("\nImage corners:")
    print(coords)

    xform = cv2.transform(np.array([coords]), perspMat)

    print(f"Target image length: {int(edge_length * num_squares) + 1}")
    print("Transformed image corners:")
    print(xform)

    # Original image corner coordinates in the new space
    xform = xform[0]
    xform = np.int32(xform)
    x0, y0, _ = np.min(xform, axis=0)
    x1, y1, _ = np.max(xform, axis=0)

    print(f"min x: {x0}, min y: {y0}")
    print(f"max x: {x1}, max y: {y1}")

    w = x1 - x0
    h = y1 - y0
    print(f"\nwidth: {w}, height: {h}")

    # Scale results back to the same dimensions as 
    # input - just as a precaution!
    scaleX = img.shape[0] / w
    scaleY = img.shape[1] / h

    scale = min(scaleX, scaleY)
    scale = 1

    w = int(w * scale)
    h = int(h * scale)
    edge_length  = edge_length * scale
    x0 = int(x0 * scale)
    y0 = int(y0 * scale)

    print(f"\nwidth: {w}, height: {h}")

    dst = img.copy()
    if w < 0 or h < 0:
        print("Width or height are negative...")
        w = img.shape[1]
        h = img.shape[0]
    else:
        dst = cv2.resize(dst, (h, w))

        # Apply translation and scaling
        translation = np.float32([
            [scale, 0, -x0], 
            [0, scale, -y0],
            [0,0,1]]);

        perspMat = translation @ perspMat

    corrected = cv2.warpPerspective(img, perspMat, (w, h), dst ,flags=cv2.INTER_LINEAR)

    # xform = cv2.transform(np.array([coords]), perspMat)
    # for coord in xform[0]:
    #     print(coord)
    #     cv2.circle(corrected, (int(coord[0]), int(coord[1])), 5, (255,255,255), 2)

    for i in range(num_squares + 1):
        cv2.line(corrected, (int(edge_length * i) - x0, 0), (int(edge_length * i - x0), corrected.shape[0]), (0,0,255), 1) 
    for i in range(num_squares + 1):
        cv2.line(corrected, (0, int(edge_length * i) - y0), (corrected.shape[1], int(edge_length * i) - y0), (0,0,255), 1)

    rect_width = int(edge_length * num_squares)
    cv2.rectangle(corrected, (-x0, -y0), (-x0 + rect_width, -y0 + rect_width), (255, 255, 255), 2)

    print("\nPerspective matrix:")
    print(perspMat)

    cv2.imshow('corrected', corrected)
    cv2.waitKey(0)

    np.savetxt("./camera.calib", perspMat)

    test = np.loadtxt("./camera.calib")

    print(test)

if __name__=="__main__":
    img_path = r"C:\Users\tsvi\Det Kongelige Akademi\ERC_TIMBER_TRACK - General\02_PROJECTS\23_11 - LinearScanner\Software\231213_build_0.0\240123ext_calib\B\B_003.png"
    get_perspective_correction(img_path, edge_length=22.2333, num_squares=6, pixels_per_mm=2)