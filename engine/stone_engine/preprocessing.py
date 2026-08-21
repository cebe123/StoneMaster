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


def interior_mask(edge_map_image, valid_mask=None):
    """
    Kenar haritasından iç alan maskesi oluşturur.
    Kenarları kapalı bölgelere dönüştürür ve içini doldurur.
    
    Args:
        edge_map_image: Kenar haritası (0-255 grayscale veya boolean)
        valid_mask: Opsiyonel geçerli alan maskesi
        
    Returns:
        Boolean numpy array: İç alan True, dış alan False
    """
    # Kenar haritasını binary yap
    if edge_map_image.dtype == bool:
        edges = edge_map_image.astype(np.uint8) * 255
    else:
        edges = (edge_map_image > 0).astype(np.uint8) * 255
    
    # Morfolojik işlemlerle kenarları kalınlaştır (boşlukları kapat)
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3))
    edges_dilated = cv2.dilate(edges, kernel, iterations=2)
    edges_closed = cv2.erode(edges_dilated, kernel, iterations=1)
    
    # Tüm beyaz kenarları birleştir
    contours, _ = cv2.findContours(edges_closed, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    
    # Boş canvas oluştur
    h, w = edges.shape[:2]
    filled = np.zeros((h, w), dtype=np.uint8)
    
    # Her konturun içini doldur
    for contour in contours:
        if cv2.contourArea(contour) > 50:  # Çok küçük konturları atla
            # Konturu kapat
            epsilon = 0.02 * cv2.arcLength(contour, True)
            approx = cv2.approxPolyDP(contour, epsilon, True)
            
            # İçini doldur
            cv2.drawContours(filled, [approx], -1, 255, thickness=cv2.FILLED)
    
    # Kenar piksellerini de dahil et (kenar çizgileri üzerinde de taş olsun)
    edges_bool = edges > 0
    filled_bool = filled > 0
    
    # Kenarları ve iç alanı birleştir
    result = np.logical_or(edges_bool, filled_bool)
    
    # Valid mask varsa uygula
    if valid_mask is not None:
        result = np.logical_and(result, valid_mask)
    
    return result
