from dataclasses import asdict, dataclass
from typing import Tuple


@dataclass(frozen=True)
class StoneDef:
    id: str
    name: str
    diameter_mm: float
    price_tl: float


@dataclass(frozen=True)
class PaletteColor:
    name: str
    hex: str
    rgb: Tuple[int, int, int]


@dataclass(frozen=True)
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
