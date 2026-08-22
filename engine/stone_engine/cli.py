import json
import sys
from pathlib import Path
from .engine import run_request
from .color_quantization import analyze_dominant_colors

def main():
    # Renk analizi modu: analyze-colors <image_path> <output_path>
    if len(sys.argv) == 4 and sys.argv[1] == "analyze-colors":
        analyze_colors_cli(sys.argv[2], sys.argv[3])
        return
    
    # Normal işlem modu: request.json response.json
    if len(sys.argv) != 3:
        raise SystemExit("Usage: StoneMaster.Engine.exe request.json response.json")

    request_path, response_path = sys.argv[1], sys.argv[2]
    try:
        print("PROGRESS=5|Görsel okunuyor...", flush=True)
        with open(request_path, encoding="utf-8") as f:
            request = json.load(f)
        print("PROGRESS=20|Görsel analiz ediliyor...", flush=True)
        result = run_request(
            request,
            progress=lambda value, message: print(
                f"PROGRESS={value}|{message}", flush=True))
    except Exception as exc:
        result = {"success": False, "error": str(exc), "stone_count": 0, "total_cost_tl": 0, "used_colors": 0, "width_mm": 0, "height_mm": 0, "average_density": 0, "stones": []}

    with open(response_path, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False)
    print("PROGRESS=100|İşlem tamamlandı", flush=True)
    print(f"STONE_COUNT={result.get('stone_count', 0)}")
    print(f"COST={result.get('total_cost_tl', 0)}")

def analyze_colors_cli(image_path: str, output_path: str):
    """
    Görseldeki baskın renkleri analiz edip JSON formatında kaydeder.
    Çıktı formatı: {"colors": [{"name": "...", "hex": "#RRGGBB", "percentage": XX.X}], "total_colors": N}
    """
    try:
        colors = analyze_dominant_colors(image_path, max_colors=12)
        
        result = {
            "colors": colors,
            "total_colors": len(colors)
        }
        
        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(result, f, ensure_ascii=False, indent=2)
            
        print(f"✅ {len(colors)} renk analiz edildi.", flush=True)
        
    except Exception as exc:
        print(f"❌ Hata: {exc}", flush=True)
        # Hata durumunda boş sonuç döndür
        result = {"colors": [], "total_colors": 0}
        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(result, f, ensure_ascii=False)

if __name__ == "__main__":
    main()
