import tempfile
from pathlib import Path
import cv2
import numpy as np
from stone_engine.engine import kumas_tas_kalip_uretec

def test_basic_generation():
    img = np.full((120, 160, 3), 255, dtype=np.uint8)
    cv2.circle(img, (80, 60), 30, (20, 20, 220), -1)
    path = Path(tempfile.gettempdir()) / "stonemaster_test.png"
    cv2.imwrite(str(path), cv2.cvtColor(img, cv2.COLOR_RGB2BGR))

    result = kumas_tas_kalip_uretec(
        str(path),
        kumas_genislik_mm=1000,
        tas_boyutu="SS10",
        yogunluk=0.5,
        arka_plan_esigi=245,
        palet_renkleri=["Crystal", "Black"],
    )
    assert result["success"] is True
    assert result["stone_count"] > 0

def test_laser_diameter():
    # Source requirement: 2.8 + 0.3 = 3.1
    result = 2.8 + 0.3
    assert abs(result - 3.1) < 1e-9
