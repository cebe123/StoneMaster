# StoneMaster - Rhinestone Tasarım Otomasyonu

CorelDRAW için geliştirilmiş, görsellerdeki desenleri otomatik olarak analiz edip rhinestone (yapay elmas) yerleştirme haritası oluşturan profesyonel eklenti.

## 🚀 Hızlı Başlangıç

### Gereksinimler
- Windows 10/11
- CorelDRAW Graphics Suite 2023 veya 2024
- .NET Framework 4.8
- Python 3.8+
- Visual Studio 2022 Build Tools veya MSBuild

### Derleme

#### Tüm Projeyi Derle
```powershell
.\scripts\build.ps1
```

#### Sadece Corel Eklentisini Derle
```powershell
.\scripts\build-corel.ps1
```

#### Özel Corel Yolu ile Derle
```powershell
.\scripts\build.ps1 -CorelInteropPath "C:\Program Files\Corel\CorelDRAW Graphics Suite 2024\Programs64\Corel.Interop.VGCore.dll"
```

#### Engine Olmadan Derle (Sadece Corel)
```powershell
.\scripts\build.ps1 -SkipEngineBuild
```

### Kurulum

Derleme tamamlandıktan sonra:

```powershell
.\scripts\package.ps1
```

Bu komut kurulum paketini oluşturur. `artifacts/installer` klasöründeki `.exe` dosyasını çalıştırarak kurulum yapabilirsiniz.

## 📁 Proje Yapısı

```
StoneMaster/
├── corel/                  # CorelDRAW eklentisi
│   └── StoneMaster.Corel/  # .NET add-on projesi
├── engine/                 # Görüntü işleme motoru (Python)
│   └── stone_engine/       # Ana işlem kodları
├── scripts/                # PowerShell derleme betikleri
│   ├── build.ps1           # Tüm projeyi derler
│   ├── build-corel.ps1     # Sadece Corel eklentisini derler
│   ├── build-engine.ps1    # Sadece Python engine'i derler
│   └── package.ps1         # Kurulum paketi oluşturur
├── artifacts/              # Derleme çıktıları
├── config/                 # Varsayılan ayarlar
└── docs/                   # Dokümantasyon
```

## 🎯 Özellikler

### 1. Otomatik Desen Tanıma
- Renk analizi ile desen sınırlarını belirleme
- Şekil algılama algoritmaları
- Kenar tespiti ve kontur çıkarma

### 2. Akıllı Taş Yerleştirme
- Otomatik taş boyutu seçimi
- Yoğunluk optimizasyonu
- Renk uyumlu taş paleti önerisi

### 3. CorelDRAW Entegrasyonu
- Doğrudan CorelDRAW menüsünden erişim
- Vektörel çıktı oluşturma
- Katman yönetimi

### 4. Kullanıcı Dostu Arayüz
- Tek tıkla işlem akışı
- Gerçek zamanlı önizleme
- Taş sayısı ve maliyet hesaplama

## 🔧 Kullanım

### Adım 1: Görsel Yükle
CorelDRAW'da bir görsel açın veya yeni görsel içe aktarın.

### Adım 2: Deseni Analiz Et
StoneMaster menüsünden "Desen Analizi" seçeneğini tıklayın. Sistem otomatik olarak:
- Desen sınırlarını belirler
- Renk bölgelerini ayırır
- Uygun taş boyutlarını önerir

### Adım 3: Ayarları Yap (İsteğe Bağlı)
- Taş boyutu (ss - stone size)
- Taş yoğunluğu
- Renk paleti

### Adım 4: Taş Haritasını Oluştur
"Taş Yerleştir" butonuna tıklayın. Önizleme ekranında sonucu görün.

### Adım 5: CorelDRAW'a Aktar
Onayladıktan sonra vektörel taş haritası CorelDRAW'a aktarılır.

## ⚙️ Yapılandırma

### Taş Boyutları (`config/stones.json`)
```json
{
  "sizes": [
    {"name": "SS5", "mm": 1.8},
    {"name": "SS6", "mm": 2.0},
    {"name": "SS10", "mm": 2.8}
  ]
}
```

### Renk Paleti (`config/palette.json`)
Kullanılabilir rhinestone renklerini tanımlayın.

## 🛠️ Sorun Giderme

### "Corel.Interop.VGCore.dll bulunamadı"
1. CorelDRAW'ın yüklü olduğundan emin olun
2. DLL yolunu manuel belirtin:
   ```powershell
   .\scripts\build.ps1 -CorelInteropPath "C:\path\to\Corel.Interop.VGCore.dll"
   ```

### "MSBuild bulunamadı"
- Visual Studio 2022 Build Tools yükleyin
- Veya .NET SDK kurun

### Engine Derleme Hatası
```powershell
python -m pip install -r engine/requirements.txt
python -m pip install pyinstaller
.\scripts\build-engine.ps1
```

## 📝 Lisans

Proprietary - Tüm hakları saklıdır.

## 🤝 Destek

Sorularınız için: [destek@stonemaster.com](mailto:destek@stonemaster.com)

---

**Not:** Bu yazılım profesyonel rhinestone uygulaması yapan işletmeler için tasarlanmıştır. Test amaçlı kullanımda lütfen demo görseller kullanın.
