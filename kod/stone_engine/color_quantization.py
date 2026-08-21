import cv2
import numpy as np

def rgb_to_lab(rgb):
    arr = np.array([[rgb]], dtype=np.uint8)
    lab = cv2.cvtColor(arr, cv2.COLOR_RGB2LAB)[0,0]
    return lab.astype(np.float32)

def nearest_palette(rgb, palette, algorithm="Lab"):
    rgb = np.asarray(rgb, dtype=np.float32)
    best = None
    best_d = float("inf")
    if algorithm.upper() == "LAB":
        p = rgb_to_lab(rgb.astype(np.uint8))
    for color in palette:
        q = np.asarray(color.rgb, dtype=np.float32)
        if algorithm.upper() == "LAB":
            q = rgb_to_lab(q.astype(np.uint8))
        d = float(np.linalg.norm(p - q)) if algorithm.upper() == "LAB" else float(np.linalg.norm(rgb - q))
        if d < best_d:
            best_d = d
            best = color
    return best, best_d
