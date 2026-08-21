import cv2
import numpy as np

def preprocess(image, noise_reduction=True, contrast=True, sharpen=False):
    out = image.copy()
    if noise_reduction:
        out = cv2.GaussianBlur(out, (3, 3), 0)
    if contrast:
        lab = cv2.cvtColor(out, cv2.COLOR_RGB2LAB)
        l, a, b = cv2.split(lab)
        clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
        l = clahe.apply(l)
        out = cv2.cvtColor(cv2.merge([l, a, b]), cv2.COLOR_LAB2RGB)
    if sharpen:
        kernel = np.array([[0,-1,0],[-1,5,-1],[0,-1,0]], dtype=np.float32)
        out = cv2.filter2D(out, -1, kernel)
    return out

def luminance(image):
    return cv2.cvtColor(image, cv2.COLOR_RGB2GRAY)

def background_mask(image, threshold=245, mode="LIGHT", tolerance=28, color="#F5F5F5"):
    mode = (mode or "LIGHT").upper()
    if mode == "AUTO":
        pixels = image.reshape(-1, 3).astype(np.float32)
        border = np.concatenate((image[0], image[-1], image[:, 0], image[:, -1]), axis=0).astype(np.float32)
        background_color = np.median(border, axis=0)
        distance = np.linalg.norm(pixels - background_color, axis=1)
        return (distance > float(tolerance)).reshape(image.shape[:2])

    if mode == "COLOR":
        value = color.lstrip("#")
        if len(value) != 6:
            raise ValueError("Background color must be a hex value")
        background = np.array([int(value[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32)
        distance = np.linalg.norm(image.astype(np.float32) - background, axis=2)
        return distance > float(tolerance)

    lum = luminance(image)
    if mode == "DARK":
        return lum >= threshold
    return lum < threshold
