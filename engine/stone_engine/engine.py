"""StoneMaster image-to-rhinestone placement engine."""

from pathlib import Path
import re
from typing import Any, Dict, List, Optional, Sequence

import numpy as np

from .config import load_palette, load_stones
from .image_loader import fit_max_dimension, load_image
from .preprocessing import background_mask, preprocess
from .edge_detection import edge_map
from .stone_placement import adaptive_prune, assign_stones, create_candidates, resolve_collisions, resolve_variable_collisions, to_placements
from .budget_optimizer import optimize_budget
from .export import export_csv
from .models import PaletteColor

HEX_RE = re.compile(r"^#[0-9A-Fa-f]{6}$")


class StonePlacementEngine:
    SUPPORTED_STYLES = {"balanced", "edge", "fill", "scatter"}
    SUPPORTED_BACKGROUND_MODES = {"LIGHT", "DARK", "AUTO", "COLOR"}

    def __init__(self, config_dir: Optional[str] = None):
        self.stones = load_stones(config_dir)
        self.palette = load_palette(config_dir)

    def process(self, image_path: str, fabric_width_mm: float = 1000.0, fabric_height_mm: Optional[float] = None,
                stone_sizes: Optional[List[str]] = None, colors: Optional[List[str]] = None, style: str = "balanced",
                density: float = 0.65, gap_mm: float = 0.5, laser_tolerance_mm: float = 0.3,
                budget_tl: Optional[float] = None, stone_unit_price_tl: Optional[float] = None,
                exclude_background: bool = True, background_mode: str = "AUTO", background_threshold: int = 245,
                background_tolerance: float = 28.0, background_color: str = "#F5F5F5", edge_sensitivity: float = 0.5,
                detail_sensitivity: float = 0.5, custom_palette_hex: Optional[List[str]] = None,
                exclusion_rect: Optional[Sequence[float]] = None, sprinkle: bool = False,
                exclude_dark_stones: bool = False, dark_stone_threshold: int = 70,
                excluded_stone_groups: Optional[List[List[str]]] = None, edge_only: bool = False,
                edge_threshold: int = 80, grid_snap: bool = False, analysis_max_dimension: int = 1600,
                output_csv: Optional[str] = None, progress_callback=None) -> Dict[str, Any]:
        progress = progress_callback or (lambda _value, _message: None)
        self._validate_request(image_path=image_path, fabric_width_mm=fabric_width_mm, fabric_height_mm=fabric_height_mm,
                               stone_sizes=stone_sizes, density=density, gap_mm=gap_mm,
                               laser_tolerance_mm=laser_tolerance_mm, style=style, background_mode=background_mode,
                               background_threshold=background_threshold, background_tolerance=background_tolerance,
                               edge_sensitivity=edge_sensitivity, detail_sensitivity=detail_sensitivity,
                               edge_threshold=edge_threshold, analysis_max_dimension=analysis_max_dimension,
                               exclusion_rect=exclusion_rect, stone_unit_price_tl=stone_unit_price_tl)

        stone_sizes = stone_sizes or ["SS10"]
        selected_stones = [self.stones[name] for name in stone_sizes]
        selected_palette = self._resolve_palette(colors, custom_palette_hex)
        style = (style or "balanced").strip().lower()
        mode = (background_mode or "AUTO").upper()
        if fabric_height_mm is None:
            fabric_height_mm = self._infer_height(image_path, fabric_width_mm)

        image = load_image(image_path)
        progress(10, "Görsel yüklendi")
        original_h, original_w = image.shape[:2]
        image, _ = fit_max_dimension(image, analysis_max_dimension)
        progress(20, "Görsel optimize edildi")

        mask = background_mask(image, threshold=background_threshold, mode=mode, tolerance=background_tolerance,
                               color=background_color) if exclude_background else np.ones(image.shape[:2], dtype=bool)
        mask = self._apply_exclusion(mask, exclusion_rect)
        progress(30, "Çalışma alanı belirlendi")
        processed = preprocess(image, noise_reduction=True, contrast=True)
        progress(40, "Görsel iyileştirildi")
        edges = edge_map(processed, sensitivity=edge_sensitivity)
        progress(50, "Kenarlar tespit edildi")

        weights = self._get_style_weights(style, detail_sensitivity)
        weights.update({"fill_interior": style in {"fill", "balanced"}, "grid_snap": bool(grid_snap),
                        "interactive_mask": None, "sprinkle": bool(sprinkle or style == "scatter"),
                        "exclude_dark": bool(exclude_dark_stones), "dark_threshold": int(dark_stone_threshold),
                        "edge_only": bool(edge_only or style == "edge"), "edge_threshold": int(edge_threshold)})
        candidates = create_candidates(image=processed, valid_mask=mask, edge=edges, stones=selected_stones,
                                       palette=selected_palette, density=density, **weights)
        progress(65, "Aday noktalar oluşturuldu")
        candidates = self._filter_excluded_groups(candidates, excluded_stone_groups)
        candidates = adaptive_prune(candidates, density)
        progress(70, "Yoğunluk optimize edildi")

        image_step_mm = fabric_width_mm / max(1, processed.shape[1])
        gap_px = gap_mm / max(image_step_mm, 1e-9)
        if len(selected_stones) == 1:
            radius_px = selected_stones[0].diameter_mm / max(image_step_mm, 1e-9) / 2.0
            candidates = resolve_collisions(candidates, radius_px, gap_px)
        else:
            candidates = resolve_variable_collisions(assign_stones(candidates, selected_stones), image_step_mm, gap_mm)
        progress(80, "Çarpışmalar çözüldü")

        placements = to_placements(points=candidates, stone=selected_stones[0], width_mm=fabric_width_mm,
                                   height_mm=fabric_height_mm, image_width=processed.shape[1], image_height=processed.shape[0],
                                   laser_tolerance=laser_tolerance_mm)
        progress(90, "Yerleşim hesaplandı")

        prices = {stone.name: (float(stone_unit_price_tl) if stone_unit_price_tl is not None else stone.price_tl)
                  for stone in selected_stones}
        if budget_tl is not None:
            placements = optimize_budget(placements, prices[selected_stones[0].name], float(budget_tl), prices)
            progress(95, "Bütçe optimize edildi")
        if output_csv:
            export_csv(placements, output_csv)

        total_cost = sum(prices.get(item.stone_name, 0.0) for item in placements)
        progress(100, "Tamamlandı")
        return {"success": True, "stone_count": len(placements), "total_cost_tl": round(total_cost, 2),
                "used_colors": len({item.color_name for item in placements}), "width_mm": round(float(fabric_width_mm), 3),
                "height_mm": round(float(fabric_height_mm), 3), "average_density": round(len(placements) / max(1, len(candidates)), 4),
                "source_width_px": int(original_w), "source_height_px": int(original_h),
                "analysis_width_px": int(processed.shape[1]), "analysis_height_px": int(processed.shape[0]),
                "stones": [item.to_dict() for item in placements]}

    def _infer_height(self, image_path, width_mm):
        image = load_image(image_path); h, w = image.shape[:2]
        if w <= 0: raise ValueError("Görsel genişliği hesaplanamadı")
        return float(h * (width_mm / w))

    def _validate_request(self, **kwargs):
        path = kwargs["image_path"]
        if not path or not Path(path).is_file(): raise FileNotFoundError(f"Görsel bulunamadı: {path}")
        if kwargs["fabric_width_mm"] is None or float(kwargs["fabric_width_mm"]) <= 0: raise ValueError("Kumaş genişliği 0'dan büyük olmalıdır")
        if kwargs["fabric_height_mm"] is not None and float(kwargs["fabric_height_mm"]) <= 0: raise ValueError("Kumaş yüksekliği 0'dan büyük olmalıdır")
        if not 0.01 <= float(kwargs["density"]) <= 1.0: raise ValueError("Yoğunluk 0.01 ile 1.0 arasında olmalıdır")
        for name in ("gap_mm", "laser_tolerance_mm", "background_tolerance"):
            if float(kwargs[name]) < 0: raise ValueError(f"{name} negatif olamaz")
        if kwargs.get("stone_unit_price_tl") is not None and float(kwargs["stone_unit_price_tl"]) < 0: raise ValueError("Taş birim fiyatı negatif olamaz")
        for name in ("background_threshold", "edge_threshold", "analysis_max_dimension"):
            if int(kwargs[name]) <= 0: raise ValueError(f"{name} 0'dan büyük olmalıdır")
        for name in ("edge_sensitivity", "detail_sensitivity"):
            if not 0.0 <= float(kwargs[name]) <= 1.0: raise ValueError(f"{name} 0 ile 1 arasında olmalıdır")
        style = str(kwargs["style"] or "balanced").lower()
        if style not in self.SUPPORTED_STYLES: raise ValueError(f"Desteklenmeyen stil: {style}")
        mode = str(kwargs["background_mode"] or "AUTO").upper()
        if mode not in self.SUPPORTED_BACKGROUND_MODES: raise ValueError(f"Desteklenmeyen arka plan modu: {mode}")
        sizes = kwargs["stone_sizes"] or ["SS10"]
        invalid = [size for size in sizes if size not in self.stones]
        if invalid: raise ValueError(f"Bilinmeyen taş boyutları: {', '.join(invalid)}")
        rect = kwargs.get("exclusion_rect")
        if rect is not None and (len(rect) != 4 or any(float(v) < 0 or float(v) > 1 for v in rect)):
            raise ValueError("exclusion_rect [x,y,w,h] normalize edilmiş 0..1 değerlerinden oluşmalıdır")

    def _resolve_palette(self, colors, custom_hex):
        if custom_hex:
            result = []
            for index, value in enumerate(custom_hex):
                value = str(value).strip().upper()
                if not HEX_RE.match(value): raise ValueError(f"Geçersiz özel renk: {value}")
                rgb = tuple(int(value[i:i + 2], 16) for i in (1, 3, 5))
                result.append(PaletteColor(f"CUSTOM_{index + 1}", value, rgb))
            return result
        if colors:
            requested = {str(value).strip().lower() for value in colors}
            result = [color for color in self.palette if color.name.lower() in requested or color.hex.lower() in requested]
            if not result: raise ValueError("Seçilen renkler mevcut rhinestone paletiyle eşleşmedi")
            return result
        return list(self.palette[:4])

    @staticmethod
    def _apply_exclusion(mask, rect):
        if rect is None: return mask
        x, y, width, height = [float(v) for v in rect]; h, w = mask.shape[:2]
        left, top = max(0, min(w, int(round(x * w)))), max(0, min(h, int(round(y * h))))
        right, bottom = max(left, min(w, int(round((x + width) * w)))), max(top, min(h, int(round((y + height) * h))))
        result = mask.copy(); result[top:bottom, left:right] = False; return result

    @staticmethod
    def _filter_excluded_groups(points, excluded_groups):
        if not excluded_groups: return points
        excluded = {(str(item[0]).lower(), str(item[1]).lower()) for item in excluded_groups if len(item) >= 2}
        return [point for point in points if (point[2].name.lower(), point[2].hex.lower()) not in excluded]

    @staticmethod
    def _get_style_weights(style, detail_sensitivity):
        styles = {"balanced": (0.30, 0.15, 0.45, 0.10), "edge": (0.60, 0.10, 0.20, 0.10), "fill": (0.15, 0.10, 0.60, 0.15), "scatter": (0.20, 0.20, 0.30, 0.30)}
        edge_w, detail_w, color_w, local_w = styles[style]
        return {"edge_weight": edge_w, "detail_weight": max(0.0, min(1.0, detail_w * (0.5 + detail_sensitivity))), "color_weight": color_w, "local_weight": local_w}


def process_image(image_path, fabric_width_mm=1000.0, stone_sizes=None, colors=None, style="balanced", density=0.65, **kwargs):
    return StonePlacementEngine().process(image_path=image_path, fabric_width_mm=fabric_width_mm, stone_sizes=stone_sizes, colors=colors, style=style, density=density, **kwargs)


def kumas_tas_kalip_uretec(resim_yolu, kumas_genislik_mm=None, kumas_yukseklik_mm=None, tas_boyutu="SS10", tas_boyutlari=None,
                           tas_paleti=None, palet_renkleri=None, gap_mm=0.5, lazer_tolerans_mm=0.3, yogunluk=1.0,
                           kenar_hassasiyeti=0.5, detay_hassasiyeti=0.5, arka_plan_esigi=245, arka_plan_modu="LIGHT",
                           arka_plan_toleransi=28, calisma_modu="FULL", maliyet_hesapla=False, maliyet_kisitlamasi_aktif=False,
                           hedef_butce_tl=None, tas_birim_maliyeti_tl=None, exclusion_rect=None, arka_plan_rengi="#F5F5F5",
                           custom_palette_hex=None, serpme=False, koyu_taslari_haric=False, koyu_esik=70, excluded_stone_groups=None,
                           edge_only=False, edge_threshold=80, output_csv=None, analysis_max_dimension=1600, progress=None):
    mode = str(calisma_modu or "FULL").upper()
    style = {"FULL": "balanced", "EDGE": "edge", "FILL": "fill", "SCATTER": "scatter", "BUDGET": "balanced"}.get(mode, "balanced")
    if edge_only: style = "edge"
    if serpme: style = "scatter"
    return StonePlacementEngine().process(image_path=resim_yolu, fabric_width_mm=float(kumas_genislik_mm or 1000.0), fabric_height_mm=kumas_yukseklik_mm,
        stone_sizes=tas_boyutlari or [tas_boyutu], colors=palet_renkleri or None, style=style, density=float(yogunluk), gap_mm=float(gap_mm),
        laser_tolerance_mm=float(lazer_tolerans_mm), budget_tl=hedef_butce_tl if maliyet_kisitlamasi_aktif else None,
        stone_unit_price_tl=tas_birim_maliyeti_tl, background_mode=arka_plan_modu, background_threshold=int(arka_plan_esigi),
        background_tolerance=float(arka_plan_toleransi), background_color=arka_plan_rengi, edge_sensitivity=float(kenar_hassasiyeti),
        detail_sensitivity=float(detay_hassasiyeti), custom_palette_hex=custom_palette_hex, exclusion_rect=exclusion_rect,
        sprinkle=bool(serpme), exclude_dark_stones=bool(koyu_taslari_haric), dark_stone_threshold=int(koyu_esik),
        excluded_stone_groups=excluded_stone_groups, edge_only=bool(edge_only), edge_threshold=int(edge_threshold),
        analysis_max_dimension=int(analysis_max_dimension), output_csv=output_csv, progress_callback=progress)


def run_request(request, progress=None):
    return kumas_tas_kalip_uretec(resim_yolu=request["image_path"], kumas_genislik_mm=request.get("width_mm") or 1000.0,
        kumas_yukseklik_mm=request.get("height_mm"), tas_boyutu=request.get("stone_size", "SS10"), tas_boyutlari=request.get("stone_sizes"),
        palet_renkleri=request.get("palette_colors"), gap_mm=float(request.get("gap_mm", 0.5)), lazer_tolerans_mm=float(request.get("laser_tolerance_mm", 0.3)),
        yogunluk=float(request.get("density", 0.65)), kenar_hassasiyeti=float(request.get("edge_sensitivity", 0.5)),
        detay_hassasiyeti=float(request.get("detail_sensitivity", 0.5)), arka_plan_esigi=int(request.get("background_threshold", 245)),
        arka_plan_modu=request.get("background_mode", "AUTO"), arka_plan_toleransi=float(request.get("background_tolerance", 28)),
        calisma_modu=request.get("mode", "FULL"), maliyet_kisitlamasi_aktif=bool(request.get("budget_enabled", False)),
        hedef_butce_tl=request.get("target_budget_tl"), tas_birim_maliyeti_tl=request.get("stone_unit_price_tl"), exclusion_rect=request.get("exclusion_rect"),
        arka_plan_rengi=request.get("background_color", "#F5F5F5"), custom_palette_hex=request.get("custom_palette_hex"),
        serpme=bool(request.get("sprinkle", False)), koyu_taslari_haric=bool(request.get("exclude_dark_stones", False)), koyu_esik=int(request.get("dark_stone_threshold", 70)),
        excluded_stone_groups=request.get("excluded_stone_groups"), edge_only=bool(request.get("edge_only", False)), edge_threshold=int(request.get("edge_threshold", 80)),
        analysis_max_dimension=int(request.get("analysis_max_dimension", 1600)), progress=progress)
