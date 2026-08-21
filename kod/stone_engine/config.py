from pathlib import Path
import json
import sys
from .models import StoneDef, PaletteColor

def _config_root():
    if getattr(sys, "frozen", False):
        bundled = Path(getattr(sys, "_MEIPASS", Path.cwd())) / "config"
        if bundled.exists():
            return bundled
    return Path(__file__).resolve().parents[2] / "config"

CONFIG = _config_root()

def load_stones():
    data = json.loads((CONFIG / "stones.json").read_text(encoding="utf-8"))
    return {x["id"]: StoneDef(**x) for x in data["stones"]}

def load_palette():
    data = json.loads((CONFIG / "palette.json").read_text(encoding="utf-8"))
    return [PaletteColor(x["name"], x["hex"], tuple(x["rgb"])) for x in data["colors"]]
