from pathlib import Path
import json
import numpy as np

from .config import load_stones, load_palette
from .image_loader import load_image, fit_max_dimension
from .preprocessing import preprocess, background_mask
from .edge_detection import edge_map
from .stone_placement import create_candidates, adaptive_prune, resolve_collisions, resolve_variable_collisions, assign_stones, to_placements
from .budget_optimizer import optimize_budget
from .export import export_csv

def kumas_tas_kalip_uretec(
    resim_yolu,
    kumas_genislik_mm=None,
    kumas_yukseklik_mm=None,
    tas_boyutu="SS10",
    tas_boyutlari=None,
    tas_paleti=None,
    palet_renkleri=None,
    gap_mm=0.5,
    lazer_tolerans_mm=0.3,
    yogunluk=1.0,
    kenar_hassasiyeti=0.5,
    detay_hassasiyeti=0.5,
    arka_plan_esigi=245,
    arka_plan_modu="LIGHT",
    arka_plan_toleransi=28,
    calisma_modu="FULL",
    maliyet_hesapla=False,
    maliyet_kisitlamasi_aktif=False,
    hedef_butce_tl=None,
    tas_birim_maliyeti_tl=None,
    exclusion_rect=None,
    arka_plan_rengi="#F5F5F5",
    custom_palette_hex=None,
    serpme=False,
    koyu_taslari_haric=False,
    koyu_esik=70,
    excluded_stone_groups=None,
    edge_only=False,
    edge_threshold=80,
    output_csv=None,
    analysis_max_dimension=1600,
    progress=None,
):
    progress = progress or (lambda value, message: None)
    stones = load_stones()
    palette = load_palette() if tas_paleti is None else tas_paleti
    selected_sizes = tas_boyutlari or [tas_boyutu]
    invalid_sizes = [size for size in selected_sizes if size not in stones]
    if invalid_sizes:
        raise ValueError(f"Unknown stone sizes: {', '.join(invalid_sizes)}")
    if palet_renkleri is not None:
        palette = [item for item in palette if item.name in palet_renkleri or item.hex in palet_renkleri]
    if custom_palette_hex:
        from .models import PaletteColor
        palette.extend(
            PaletteColor(f"Custom {value}", value, tuple(int(value[i:i + 2], 16) for i in (1, 3, 5)))
            for value in custom_palette_hex
            if isinstance(value, str) and len(value) == 7 and value.startswith("#")
        )
    if not palette:
        raise ValueError("At least one palette color must be selected")
    if not selected_sizes:
        raise ValueError(f"Unknown stone size: {tas_boyutu}")

    image = load_image(resim_yolu)
    progress(30, "Görsel okundu")
    orig_h, orig_w = image.shape[:2]

    if kumas_genislik_mm is None:
        kumas_genislik_mm = float(orig_w)
    if kumas_yukseklik_mm is None:
        kumas_yukseklik_mm = float(orig_h) * (kumas_genislik_mm / orig_w)

    image, _ = fit_max_dimension(image, analysis_max_dimension)
    mask = background_mask(image, arka_plan_esigi, arka_plan_modu, arka_plan_toleransi, arka_plan_rengi)
    if koyu_taslari_haric:
        luminance = 0.299 * image[:, :, 0] + 0.587 * image[:, :, 1] + 0.114 * image[:, :, 2]
        mask &= luminance > koyu_esik
    image = preprocess(image, noise_reduction=True, contrast=True, sharpen=False)
    edges = edge_map(image, kenar_hassasiyeti)
    progress(55, "Görsel analiz edildi")
    selected_stones = [stones[size] for size in selected_sizes]
    if tas_birim_maliyeti_tl is not None:
        selected_stones = [
            type(item)(item.id, item.name, item.diameter_mm, float(tas_birim_maliyeti_tl))
            for item in selected_stones
        ]
    stone = min(selected_stones, key=lambda item: item.diameter_mm)

    candidates = create_candidates(
        image, mask, edges, stone, palette, max(0.01, yogunluk),
        edge_weight=0.30, detail_weight=0.15,
        color_weight=0.45, local_weight=0.10
        ,sprinkle=serpme,
        exclude_dark=koyu_taslari_haric,
        dark_threshold=koyu_esik,
        edge_only=edge_only and not serpme,
        edge_threshold=edge_threshold
    )

    if exclusion_rect and len(exclusion_rect) == 4:
        ex, ey, ew, eh = [float(value) for value in exclusion_rect]
        candidates = [
            point for point in candidates
            if not (ex <= point[0] / image.shape[1] <= ex + ew
                    and ey <= point[1] / image.shape[0] <= ey + eh)
        ]

    candidates = adaptive_prune(candidates, yogunluk)

    image_step_mm = kumas_genislik_mm / image.shape[1]
    radius_px = (stone.diameter_mm / image_step_mm) / 2.0
    gap_px = gap_mm / image_step_mm
    if len(selected_stones) == 1:
        candidates = resolve_collisions(candidates, radius_px, gap_px)
    else:
        candidates = resolve_variable_collisions(
            assign_stones(candidates, selected_stones),
            image_step_mm,
            gap_mm)
    progress(80, "Taş konumları hesaplandı")

    placements = to_placements(
        candidates,
        stone,
        kumas_genislik_mm,
        kumas_yukseklik_mm,
        image.shape[1],
        image.shape[0],
        lazer_tolerans_mm,
    )

    if excluded_stone_groups:
        excluded = {tuple(item) for item in excluded_stone_groups if len(item) == 2}
        placements = [
            item for item in placements
            if (item.stone_name, item.color_name) not in excluded
        ]

    prices = {item.name: item.price_tl for item in selected_stones}
    total_cost = sum(prices.get(item.stone_name, stone.price_tl) for item in placements)
    if maliyet_kisitlamasi_aktif or calisma_modu.upper() == "BUDGET":
        placements = optimize_budget(placements, stone.price_tl, hedef_butce_tl, prices)
        total_cost = sum(prices.get(item.stone_name, stone.price_tl) for item in placements)

    if output_csv:
        export_csv(placements, output_csv)

    progress(95, "Önizleme hazırlanıyor")

    used = len({p.color_name for p in placements})
    density = (len(placements) / max(1, len(candidates)))

    return {
        "success": True,
        "stone_count": len(placements),
        "total_cost_tl": round(total_cost, 2),
        "used_colors": used,
        "width_mm": round(kumas_genislik_mm, 3),
        "height_mm": round(kumas_yukseklik_mm, 3),
        "average_density": round(density, 4),
        "stones": [p.to_dict() for p in placements],
    }

def run_request(request, progress=None):
    return kumas_tas_kalip_uretec(
        resim_yolu=request["image_path"],
        kumas_genislik_mm=request.get("width_mm"),
        kumas_yukseklik_mm=request.get("height_mm"),
        tas_boyutu=request.get("stone_size", "SS10"),
        tas_boyutlari=request.get("stone_sizes"),
        palet_renkleri=request.get("palette_colors"),
        gap_mm=float(request.get("gap_mm", 0.5)),
        lazer_tolerans_mm=float(request.get("laser_tolerance_mm", 0.3)),
        yogunluk=float(request.get("density", 0.65)),
        kenar_hassasiyeti=float(request.get("edge_sensitivity", 0.5)),
        detay_hassasiyeti=float(request.get("detail_sensitivity", 0.5)),
        arka_plan_esigi=int(request.get("background_threshold", 245)),
        arka_plan_modu=request.get("background_mode", "LIGHT"),
        arka_plan_toleransi=float(request.get("background_tolerance", 28)),
        calisma_modu=request.get("mode", "FULL"),
        maliyet_hesapla=True,
        maliyet_kisitlamasi_aktif=bool(request.get("budget_enabled", False)),
        hedef_butce_tl=request.get("target_budget_tl"),
        tas_birim_maliyeti_tl=request.get("stone_unit_price_tl"),
        exclusion_rect=request.get("exclusion_rect"),
        arka_plan_rengi=request.get("background_color", "#F5F5F5"),
        custom_palette_hex=request.get("custom_palette_hex"),
        serpme=bool(request.get("sprinkle", False)),
        koyu_taslari_haric=bool(request.get("exclude_dark_stones", False)),
        koyu_esik=int(request.get("dark_stone_threshold", 70)),
        excluded_stone_groups=request.get("excluded_stone_groups"),
        edge_only=bool(request.get("edge_only", False)),
        edge_threshold=int(request.get("edge_threshold", 80)),
        analysis_max_dimension=int(request.get("analysis_max_dimension", 1600)),
        progress=progress,
    )
