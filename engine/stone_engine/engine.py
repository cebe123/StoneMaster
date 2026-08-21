"""
StoneMaster Engine - Rhinestone Placement System
Endüstriyel Standart Görüntü İşleme ve Taş Yerleştirme Motoru

Temel Özellikler:
- Otomatik desen tanıma (kenar, detay, renk)
- Akıllı taş yerleştirme (çarpışma önleme)
- Çoklu taş boyutu desteği
- Renk paleti eşleştirme (Lab color space)
- Lazer kalıp üretimi
- Bütçe optimizasyonu

Kullanım:
    Basit Mod:
        result = engine.process_image("desen.jpg", fabric_width_mm=1000)
    
    Gelişmiş Mod:
        result = engine.process_image(
            "desen.jpg",
            stone_sizes=["SS10", "SS16"],
            colors=["Crystal", "Black"],
            style="balanced",  # balanced, edge, fill, scatter
            density=0.7,
            gap_mm=0.5
        )
"""

from pathlib import Path
import json
import numpy as np
from typing import List, Optional, Dict, Any, Union

from .config import load_stones, load_palette
from .image_loader import load_image, fit_max_dimension
from .preprocessing import preprocess, background_mask
from .edge_detection import edge_map
from .stone_placement import create_candidates, adaptive_prune, resolve_collisions, resolve_variable_collisions, assign_stones, to_placements
from .budget_optimizer import optimize_budget
from .export import export_csv


class StonePlacementEngine:
    """
    Ana taş yerleştirme motoru.
    
    Örnek Kullanım:
        engine = StonePlacementEngine()
        result = engine.process(
            image_path="design.jpg",
            fabric_width_mm=1000,
            stone_sizes=["SS10"],
            colors=["Crystal", "Black"],
            style="balanced"
        )
        
        print(f"Taş sayısı: {result['stone_count']}")
        print(f"Maliyet: {result['total_cost_tl']} TL")
    """
    
    def __init__(self, config_dir: Optional[str] = None):
        """
        Motoru başlat.
        
        Args:
            config_dir: Konfigürasyon dosyalarının dizini (opsiyonel)
        """
        self.stones = load_stones(config_dir)
        self.palette = load_palette(config_dir)
    
    def process(
        self,
        image_path: str,
        fabric_width_mm: float = 1000,
        fabric_height_mm: Optional[float] = None,
        stone_sizes: List[str] = None,
        colors: List[str] = None,
        style: str = "balanced",
        density: float = 0.65,
        gap_mm: float = 0.5,
        laser_tolerance_mm: float = 0.3,
        budget_tl: Optional[float] = None,
        exclude_background: bool = True,
        background_mode: str = "auto",
        output_csv: Optional[str] = None,
        progress_callback=None
    ) -> Dict[str, Any]:
        """
        Görüntüyü işle ve taş yerleşimini hesapla.
        
        Args:
            image_path: İşlenecek görselin yolu
            fabric_width_mm: Kumaşın fiziksel genişliği (mm)
            fabric_height_mm: Kumaşın fiziksel yüksekliği (mm), opsiyonel
            stone_sizes: Kullanılacak taş boyutları ["SS10", "SS16"]
            colors: Kullanılacak renkler ["Crystal", "Black"]
            style: Yerleştirme stili ("balanced", "edge", "fill", "scatter")
            density: Taş yoğunluğu (0.1 - 1.0)
            gap_mm: Taşlar arası boşluk (mm)
            laser_tolerance_mm: Lazer kesim toleransı (mm)
            budget_tl: Maksimum bütçe (TL), opsiyonel
            exclude_background: Arka planı hariç tut
            background_mode: Arka plan modu ("light", "dark", "auto", "color")
            output_csv: CSV çıktı dosya yolu, opsiyonel
            progress_callback: İlerleme callback fonksiyonu
            
        Returns:
            Sonuç dictionary:
                - success: bool
                - stone_count: int
                - total_cost_tl: float
                - used_colors: int
                - width_mm: float
                - height_mm: float
                - stones: List[Dict]
        """
        progress = progress_callback or (lambda v, m: None)
        
        # Varsayılan değerleri ayarla
        if stone_sizes is None:
            stone_sizes = ["SS10"]
        if colors is None:
            colors = [c.name for c in self.palette[:4]]  # İlk 4 renk
        
        # Parametre doğrulama
        self._validate_parameters(stone_sizes, colors)
        
        # Görsel yükle
        image = load_image(image_path)
        progress(10, "Görsel yüklendi")
        
        orig_h, orig_w = image.shape[:2]
        
        # Yükseklik otomatik hesaplama
        if fabric_height_mm is None:
            fabric_height_mm = orig_h * (fabric_width_mm / orig_w)
        
        # Görsel boyutunu optimize et
        image, _ = fit_max_dimension(image, 1600)
        progress(20, "Görsel optimize edildi")
        
        # Arka plan maskesi
        mask = background_mask(
            image, 
            threshold=245, 
            mode=background_mode.upper(),
            tolerance=28
        )
        progress(30, "Arka plan analiz edildi")
        
        # Ön işleme
        image = preprocess(image, noise_reduction=True, contrast=True)
        progress(40, "Görsel iyileştirildi")
        
        # Kenar algılama
        edges = edge_map(image, sensitivity=0.5)
        progress(50, "Kenarlar tespit edildi")
        
        # Stil bazlı ağırlıklar ve fill_interior parametresi
        weights = self._get_style_weights(style)
        
        # fill_interior: serpme (sprinkle) ve sadece kenar (edge_only) hariç tüm modlarda iç alanı doldur
        # Bu sayede seçili kenarların DIŞI değil İÇİ doldurulur
        fill_interior = not style.lower() in ["scatter", "edge"]
        weights['fill_interior'] = fill_interior
        
        # Aday noktaları oluştur
        selected_stones = [self.stones[size] for size in stone_sizes]
        selected_colors = [c for c in self.palette if c.name in colors]
        
        candidates = create_candidates(
            image=image,
            valid_mask=mask,
            edge=edges,
            stones=selected_stones,
            palette=selected_colors,
            density=density,
            **weights
        )
        progress(65, "Aday noktalar oluşturuldu")
        
        # Yoğunluk ayarı
        candidates = adaptive_prune(candidates, density)
        progress(70, "Yoğunluk optimize edildi")
        
        # Çarpışma çözme
        image_step_mm = fabric_width_mm / image.shape[1]
        gap_px = gap_mm / image_step_mm
        
        if len(selected_stones) == 1:
            radius_px = (selected_stones[0].diameter_mm / image_step_mm) / 2.0
            candidates = resolve_collisions(candidates, radius_px, gap_px)
        else:
            candidates = resolve_variable_collisions(
                assign_stones(candidates, selected_stones),
                image_step_mm,
                gap_mm
            )
        progress(80, "Çarpışmalar çözüldü")
        
        # Yerleşimlere dönüştür
        placements = to_placements(
            points=candidates,
            stone=selected_stones[0],
            width_mm=fabric_width_mm,
            height_mm=fabric_height_mm,
            image_width=image.shape[1],
            image_height=image.shape[0],
            laser_tolerance=laser_tolerance_mm
        )
        progress(90, "Yerleşim hesaplandı")
        
        # Bütçe optimizasyonu
        if budget_tl is not None:
            prices = {s.name: s.price_tl for s in selected_stones}
            placements = optimize_budget(placements, selected_stones[0].price_tl, budget_tl, prices)
            progress(95, "Bütçe optimize edildi")
        
        # CSV export
        if output_csv:
            export_csv(placements, output_csv)
        
        # Sonuçları hazırla
        used_colors = len({p.color_name for p in placements})
        
        result = {
            "success": True,
            "stone_count": len(placements),
            "total_cost_tl": round(sum(
                next((s.price_tl for s in selected_stones if s.name == p.stone_name), 0)
                for p in placements
            ), 2),
            "used_colors": used_colors,
            "width_mm": round(fabric_width_mm, 3),
            "height_mm": round(fabric_height_mm, 3),
            "average_density": round(len(placements) / max(1, len(candidates)), 4),
            "stones": [p.to_dict() for p in placements],
        }
        
        progress(100, "Tamamlandı")
        return result
    
    def _validate_parameters(self, stone_sizes: List[str], colors: List[str]):
        """Parametreleri doğrula."""
        invalid_sizes = [s for s in stone_sizes if s not in self.stones]
        if invalid_sizes:
            raise ValueError(f"Bilinmeyen taş boyutları: {', '.join(invalid_sizes)}")
        
        if not colors:
            raise ValueError("En az bir renk seçilmelidir")
    
    def _get_style_weights(self, style: str) -> Dict[str, float]:
        """Stil bazlı ağırlıkları döndür."""
        styles = {
            "balanced": {"edge_weight": 0.30, "detail_weight": 0.15, "color_weight": 0.45, "local_weight": 0.10},
            "edge": {"edge_weight": 0.60, "detail_weight": 0.10, "color_weight": 0.20, "local_weight": 0.10},
            "fill": {"edge_weight": 0.15, "detail_weight": 0.10, "color_weight": 0.60, "local_weight": 0.15},
            "scatter": {"edge_weight": 0.20, "detail_weight": 0.20, "color_weight": 0.30, "local_weight": 0.30},
        }
        return styles.get(style.lower(), styles["balanced"])


# Kolay kullanım için wrapper fonksiyon
def process_image(
    image_path: str,
    fabric_width_mm: float = 1000,
    stone_sizes: List[str] = None,
    colors: List[str] = None,
    style: str = "balanced",
    density: float = 0.65,
    **kwargs
) -> Dict[str, Any]:
    """
    Basit arayüz ile görüntü işleme.
    
    Örnek:
        result = process_image(
            "desen.jpg",
            fabric_width_mm=1000,
            stone_sizes=["SS10"],
            colors=["Crystal", "Black"],
            style="balanced"
        )
    """
    engine = StonePlacementEngine()
    return engine.process(
        image_path=image_path,
        fabric_width_mm=fabric_width_mm,
        stone_sizes=stone_sizes,
        colors=colors,
        style=style,
        density=density,
        **kwargs
    )


# Geriye dönük uyumluluk için eski API
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
    """Eski API - geriye dönük uyumluluk için."""
    # Yeni API'ye çevir
    stone_sizes = tas_boyutlari or [tas_boyutu]
    colors = palet_renkleri or []
    
    # Stil belirleme
    if edge_only:
        style = "edge"
    elif serpme:
        style = "scatter"
    elif yogunluk > 0.8:
        style = "fill"
    else:
        style = "balanced"
    
    engine = StonePlacementEngine()
    return engine.process(
        image_path=resim_yolu,
        fabric_width_mm=kumas_genislik_mm,
        fabric_height_mm=kumas_yukseklik_mm,
        stone_sizes=stone_sizes,
        colors=colors,
        style=style,
        density=yogunluk,
        gap_mm=gap_mm,
        laser_tolerance_mm=lazer_tolerans_mm,
        budget_tl=hedef_butce_tl if maliyet_kisitlamasi_aktif else None,
        background_mode=arka_plan_modu,
        output_csv=output_csv,
        progress_callback=progress,
    )


def run_request(request, progress=None):
    """JSON request işleme - geriye dönük uyumluluk."""
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
