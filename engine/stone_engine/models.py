from dataclasses import dataclass, asdict
from typing import List, Optional

@dataclass
class StoneDef:
    id: str
    name: str
    diameter_mm: float
    price_tl: float

@dataclass
class PaletteColor:
    name: str
    hex: str
    rgb: tuple[int, int, int]

@dataclass
class StonePlacement:
    x_mm: float
    y_mm: float
    diameter_mm: float
    laser_diameter_mm: float
    stone_name: str
    color_name: str
    hex_color: str
    importance: float

    def to_dict(self):
        return asdict(self)
