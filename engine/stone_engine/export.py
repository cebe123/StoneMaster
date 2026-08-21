import csv
import json
from dataclasses import asdict

def export_csv(placements, path):
    with open(path, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["X_MM","Y_MM","GERCEK_CAP","LAZER_CAP","TAS_ADI","HEX_RENK","IMPORTANCE"])
        for p in placements:
            writer.writerow([p.x_mm,p.y_mm,p.diameter_mm,p.laser_diameter_mm,p.stone_name,p.hex_color,p.importance])

def export_json(data, path):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
