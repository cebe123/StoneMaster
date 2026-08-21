import cv2
import numpy as np

def load_image(path: str):
    image = cv2.imread(path, cv2.IMREAD_COLOR)
    if image is None:
        raise FileNotFoundError(f"Image not readable: {path}")
    return cv2.cvtColor(image, cv2.COLOR_BGR2RGB)

def fit_max_dimension(image, max_dimension: int):
    h, w = image.shape[:2]
    scale = min(1.0, max_dimension / max(h, w))
    if scale == 1.0:
        return image, 1.0
    out = cv2.resize(image, (round(w * scale), round(h * scale)), interpolation=cv2.INTER_AREA)
    return out, scale
