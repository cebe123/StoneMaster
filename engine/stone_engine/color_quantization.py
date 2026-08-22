import cv2
import numpy as np
from pathlib import Path

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

def analyze_dominant_colors(image_path: str, max_colors: int = 12):
    """
    Görseldeki baskın renkleri analiz eder.
    
    Args:
        image_path: Görsel dosya yolu
        max_colors: Döndürülecek maksimum renk sayısı
        
    Returns:
        List[dict]: [{"name": "...", "hex": "#RRGGBB", "percentage": XX.X}, ...]
    """
    # Görseli yükle
    img = cv2.imread(image_path)
    if img is None:
        raise ValueError(f"Görsel yüklenemedi: {image_path}")
    
    # RGB'ye çevir
    img_rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    
    # Pixelleri düzleştir
    pixels = img_rgb.reshape(-1, 3).astype(np.float32)
    
    # K-means ile renk kümeleme
    criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 10, 1.0)
    flags = cv2.KMEANS_RANDOM_CENTERS
    
    # Küme sayısını belirle (max_colors kadar)
    K = max_colors
    compactness, labels, centers = cv2.kmeans(
        pixels, K, None, criteria, 10, flags
    )
    
    # Her kümenin yüzdesini hesapla
    unique_labels, counts = np.unique(labels, return_counts=True)
    total_pixels = len(labels)
    
    # Renk bilgilerini topla
    colors = []
    for i, (label, count) in enumerate(zip(unique_labels, counts)):
        percentage = (count / total_pixels) * 100
        
        # Sadece %1'den büyük renkleri al
        if percentage < 1.0:
            continue
            
        center = centers[i].astype(np.uint8)
        hex_color = "#{:02X}{:02X}{:02X}".format(int(center[0]), int(center[1]), int(center[2]))
        
        # Renk adı bul (basit yaklaşım)
        color_name = get_color_name(center)
        
        colors.append({
            "name": color_name,
            "hex": hex_color,
            "percentage": round(percentage, 1)
        })
    
    # Yüzdeye göre sırala (büyükten küçüğe)
    colors.sort(key=lambda x: x["percentage"], reverse=True)
    
    return colors[:max_colors]

def get_color_name(rgb: np.ndarray) -> str:
    """
    RGB değerinden basit renk adı tahmini yapar.
    """
    r, g, b = int(rgb[0]), int(rgb[1]), int(rgb[2])
    
    # Gri tonları
    if abs(r - g) < 20 and abs(g - b) < 20 and abs(r - b) < 20:
        if r < 50:
            return "Siyah"
        elif r > 200:
            return "Beyaz"
        else:
            return "Gri"
    
    # Ana renkler
    max_val = max(r, g, b)
    min_val = min(r, g, b)
    diff = max_val - min_val
    
    if diff < 30:
        return "Gri Tonu"
    
    # Hue hesaplama
    if max_val == r:
        h = 60 * (((g - b) / diff) % 6)
    elif max_val == g:
        h = 60 * (((b - r) / diff) + 2)
    else:
        h = 60 * (((r - g) / diff) + 4)
    
    if h < 0:
        h += 360
    
    # Renk isimlendirme
    if h < 15 or h >= 345:
        return "Kırmızı"
    elif h < 45:
        return "Turuncu"
    elif h < 75:
        return "Sarı"
    elif h < 165:
        return "Yeşil"
    elif h < 255:
        return "Mavi"
    elif h < 285:
        return "Mor"
    else:
        return "Pembe"
