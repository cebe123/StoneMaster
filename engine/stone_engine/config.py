from pathlib import Path
import json
import sys
from typing import Optional
from .models import StoneDef, PaletteColor

def _config_root(config_dir: Optional[str] = None):
    if config_dir:
        return Path(config_dir)
    if getattr(sys, "frozen", False):
        bundled = Path(getattr(sys, "_MEIPASS", Path.cwd())) / "config"
        if bundled.exists():
            return bundled
    return Path(__file__).resolve().parents[2] / "config"

CONFIG = _config_root()

def load_stones(config_dir: Optional[str] = None):
    root = _config_root(config_dir)
    data = json.loads((root / "stones.json").read_text(encoding="utf-8"))
    return {x["id"]: StoneDef(**x) for x in data["stones"]}

def load_palette(config_dir: Optional[str] = None):
    root = _config_root(config_dir)
    data = json.loads((root / "palette.json").read_text(encoding="utf-8"))
    return [PaletteColor(x["name"], x["hex"], tuple(x["rgb"])) for x in data["colors"]]
