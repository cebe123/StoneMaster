# StoneMaster Mimarisi ve Tasarım Felsefesi

## 🎯 Proje Amacı

StoneMaster, CorelDRAW içinde çalışan bir rhinestone (yapay elmas) ve trok tasarım eklentisidir. Kullanıcılar kumaş/desen görsellerini yükleyerek otomatik taş yerleşim haritası oluşturabilir ve bunu CorelDRAW'da vektörel olarak uygulayabilir.

## 🏗️ Mimari Yapı

```
┌─────────────────────────────────────────────────────────────┐
│                    CorelDRAW Host                           │
├─────────────────────────────────────────────────────────────┤
│  ┌───────────────────────────────────────────────────────┐  │
│  │         StoneMaster Docker (WPF UI)                   │  │
│  │  • Görsel Yükleme / Seçme                             │  │
│  │  • Parametre Ayarları (Sadeleştirilmiş)               │  │
│  │  • Gerçek Zamanlı Önizleme                            │  │
│  │  • CorelDRAW'a Uygulama                               │  │
│  └───────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│  ┌───────────────────────────────────────────────────────┐  │
│  │      Python Engine (IPC - JSON)                       │  │
│  │  • Görüntü İşleme (OpenCV)                            │  │
│  │  • Desen Tanıma (Edge Detection)                      │  │
│  │  • Renk Eşleştirme (Lab Color Space)                  │  │
│  │  • Taş Yerleştirme (Collision Detection)              │  │
│  │  • Lazer Kalıp Üretimi                                │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## 🔄 Kullanıcı Workflow'u (Yeniden Tasarlandı)

### 1. BASİT MOD (Varsayılan - Yeni Kullanıcılar İçin)
```
1. Görsel Seç → 2. Kumaş Genişliği Gir → 3. Taş Boyutu Seç → 4. "Otomatik" Butonuna Bas
```

### 2. GELİŞMİŞ MOD (Deneyimli Kullanıcılar İçin)
```
1. Görsel Seç/Yükle
2. Ölçeklendirme (Kumaş genişliği)
3. Taş Konfigürasyonu (Boyut + Renk Paleti)
4. Stil Seçimi (Kenar Odaklı / Dolgu / Serpme)
5. Yoğunluk Ayarı (Tek Slider)
6. Önizleme ve Düzenleme
7. CorelDRAW'a Uygula
```

## 📦 Modül Yapısı

### Python Engine (`engine/stone_engine/`)
| Modül | Sorumluluk |
|-------|-----------|
| `engine.py` | Ana API - Tüm iş akışını koordine eder |
| `preprocessing.py` | Görsel iyileştirme, arka plan maskeleme |
| `edge_detection.py` | Kenar algılama (Canny) |
| `color_quantization.py` | Renk eşleştirme (Lab RGB) |
| `stone_placement.py` | Taş yerleştirme, çarpışma çözme |
| `density_optimizer.py` | Yoğunluk optimizasyonu |
| `budget_optimizer.py` | Maliyet bazlı optimizasyon |
| `export.py` | CSV/JSON dışa aktarım |
| `models.py` | Veri modelleri |
| `config.py` | Konfigürasyon yönetimi |

### CorelDRAW Docker (`corel/StoneMaster.Corel/`)
| Modül | Sorumluluk |
|-------|-----------|
| `StoneDocker.xaml` | UI Tanımı |
| `StoneDockerViewModel.cs` | MVVM ViewModel |
| `CorelDrawService.cs` | CorelDRAW API entegrasyonu |
| `PythonEngineService.cs` | Python IPC yönetimi |
| `StoneRenderer.cs` | Vektörel rendering |
| `LayerService.cs` | Katman yönetimi |
| `CoordinateMapper.cs` | Koordinat dönüşümü |

## ⚙️ Yeniden Tasarlanan Parametre Sistemi

### ÖNCE (Karmaşık - 40+ Parametre)
```
- tas_boyutu, tas_boyutlari, tas_paleti, palet_renkleri
- gap_mm, lazer_tolerans_mm, yogunluk
- kenar_hassasiyeti, detay_hassasiyeti, arka_plan_esigi
- arka_plan_modu, arka_plan_toleransi, arka_plan_rengi
- calisma_modu, maliyet_hesapla, maliyet_kisitlamasi_aktif
- hedef_butce_tl, tas_birim_maliyeti_tl
- exclusion_rect, custom_palette_hex, serpme
- koyu_taslari_haric, koyu_esik, excluded_stone_groups
- edge_only, edge_threshold, analysis_max_dimension
```

### SONRA (Sadeleştirilmiş - 8 Ana Parametre)
```yaml
Görsel:
  - image_source: "Seçili Bitmap" | "Dosya"
  - fabric_width_mm: 1000 (varsayılan)

Taşlar:
  - stone_sizes: ["SS10"] (çoklu seçim)
  - color_palette: "Auto" | "Custom" (renk seçici)

Stil:
  - placement_style: "Balanced" | "Edge" | "Fill" | "Scatter"
  - density: 0.65 (tek slider %10-%100)

Gelişmiş:
  - gap_mm: 0.5 (varsayılan)
  - laser_tolerance_mm: 0.3 (varsayılan)
  - budget_mode: false | TL değeri
```

## 🎨 UI İyileştirmeleri

### Ana Panel Grupları
1. **GÖRSEL** (Üst)
   - [Seçili Bitmap Kullan] [Görsel Seç]
   - Aktif görsel önizlemesi

2. **ÖLÇEK** (Sol)
   - Kumaş Genişliği (mm) - Textbox
   - Otomatik yükseklik hesaplama

3. **TAŞLAR** (Orta)
   - Taş Boyutları - Grid Checkbox (SS6-SS34)
   - Renk Paleti - Renkli swatch'ler ile çoklu seçim
   - [Fotoğraftan Renk Al] [Renk Hariç Tut]

4. **STİL** (Sağ)
   - Yerleştirme Stili - Radio Buttons (Balanced/Edge/Fill/Scatter)
   - Yoğunluk - Slider (%10-%100)
   - [Canlı Önizleme] Toggle

5. **SONUÇ** (Alt)
   - Taş Sayısı, Maliyet, Renk Sayısı
   - [ÖNİZLEME] [CORELDRAW'A UYGULA]
   - Progress Bar + İptal

## 🔧 Teknik İyileştirmeler

### 1. Performans
- Büyük görseller için pyramid processing
- Async/await tam destek
- Background worker ile UI donmasını engelleme
- 100K+ taş için batch rendering

### 2. Hata Yönetimi
- Kullanıcı dostu hata mesajları (Türkçe)
- Graceful degradation
- Auto-retry mekanizması
- Log sistemi

### 3. Genişletilebilirlik
- Plugin mimarisi (yeni stone tipleri)
- Custom palette import/export
- Preset kaydetme/yükleme
- Batch processing desteği

### 4. Kalite
- Unit test coverage >80%
- Integration test suite
- Performance benchmarking
- Memory leak detection

## 📊 Çıktı Formatları

### CorelDRAW Layer'ları
1. **ORIGINAL_FABRIC** - Orijinal görsel (referans)
2. **STONE_PREVIEW** - Renkli doldurulmuş taşlar (görsel)
3. **LAZER_KALIP_KESIM** - Kırmızı hairline circles (üretim)

### Dışa Aktarım
- CSV (koordinat + renk + boyut)
- JSON (full data)
- DXF (lazer kesim için)

## 🔐 Güvenlik ve Lisans

- CorelDRAW SDK lisansı kullanıcıya aittir
- VGCore.dll dağıtılmaz
- Python engine PyInstaller ile paketlenir
- User settings encryption (opsiyonel)

## 📈 Gelecek Özellikler

- AI-based pattern recognition
- Multi-design nesting
- Real-time collaboration
- Cloud preset library
- Mobile preview app
