import json
import sys
from .engine import run_request

def main():
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

if __name__ == "__main__":
    main()
