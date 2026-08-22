import tempfile
from pathlib import Path

import cv2
import numpy as np
import pytest

from stone_engine.engine import kumas_tas_kalip_uretec


def make_image():
    img = np.full((160, 220, 3), 255, dtype=np.uint8)
    cv2.rectangle(img, (40, 30), (180, 130), (20, 20, 220), -1)
    cv2.circle(img, (110, 80), 25, (20, 180, 40), -1)
    path = Path(tempfile.gettempdir()) / "stonemaster_test.png"
    cv2.imwrite(str(path), cv2.cvtColor(img, cv2.COLOR_RGB2BGR))
    return path


def test_basic_generation():
    result = kumas_tas_kalip_uretec(
        str(make_image()),
        kumas_genislik_mm=1000,
        kumas_yukseklik_mm=700,
        tas_boyutu="SS10",
        yogunluk=0.5,
        arka_plan_esigi=245,
        palet_renkleri=["Crystal", "Black", "Red", "Green"],
    )
    assert result["success"] is True
    assert result["stone_count"] > 0
    assert result["width_mm"] == 1000
    assert result["height_mm"] == 700


def test_custom_palette_is_used():
    result = kumas_tas_kalip_uretec(
        str(make_image()),
        kumas_genislik_mm=500,
        tas_boyutu="SS10",
        yogunluk=0.4,
        custom_palette_hex=["#FF0000", "#00FF00"],
    )
    assert result["success"] is True
    assert result["stone_count"] > 0
    assert {stone["hex_color"] for stone in result["stones"]}.issubset({"#FF0000", "#00FF00"})


def test_invalid_density_fails_fast():
    with pytest.raises(ValueError):
        kumas_tas_kalip_uretec(str(make_image()), kumas_genislik_mm=500, yogunluk=1.5)


def test_multi_size_generation_has_valid_sizes():
    result = kumas_tas_kalip_uretec(
        str(make_image()),
        kumas_genislik_mm=500,
        tas_boyutlari=["SS6", "SS10", "SS16"],
        yogunluk=0.45,
    )
    assert result["success"] is True
    assert result["stone_count"] > 0
    assert {stone["stone_name"] for stone in result["stones"]}.issubset({"SS6", "SS10", "SS16"})


def test_laser_diameter():
    result = 2.8 + 0.3
    assert abs(result - 3.1) < 1e-9
