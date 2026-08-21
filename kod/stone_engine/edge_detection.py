import cv2

def edge_map(image, sensitivity=0.5):
    gray = cv2.cvtColor(image, cv2.COLOR_RGB2GRAY)
    low = int(30 + sensitivity * 70)
    high = int(100 + sensitivity * 120)
    return cv2.Canny(gray, low, high)
