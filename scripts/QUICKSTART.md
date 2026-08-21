# Hızlı Başlangıç Kılavuzu - StoneMaster Derleme

## Sorun: build.ps1 Çalıştırınca "build-corel.ps1 bulunamadı" Hatası

Bu sorun, `build.ps1` scriptinin eski sürümünde `build-corel.ps1` dosyasına referans verilmesinden kaynaklanıyordu. Bu sorun düzeltilmiştir.

## Çözüm

Artık aşağıdaki komutları kullanabilirsiniz:

### 1. Tüm Projeyi Derlemek İçin
```powershell
cd C:\path\to\StoneMaster
.\scripts\build.ps1
```

### 2. Sadece Corel Eklentisini Derlemek İçin
```powershell
.\scripts\build-corel.ps1
```

### 3. Corel Yolu Manuel Belirtmek İçin
```powershell
.\scripts\build.ps1 -CorelInteropPath "C:\Program Files\Corel\CorelDRAW Graphics Suite 2024\Programs64\Corel.Interop.VGCore.dll"
```

### 4. Engine Olmadan (Sadece Corel) Derlemek İçin
```powershell
.\scripts\build.ps1 -SkipEngineBuild
```

## Script Dosyaları

| Dosya | Açıklama |
|-------|----------|
| `build.ps1` | Tüm projeyi derler (Engine + Corel) |
| `build-corel.ps1` | Sadece Corel eklentisini derler |
| `build-engine.ps1` | Sadece Python engine'i derler |
| `package.ps1` | Kurulum paketi oluşturur |

## Yaygın Hatalar ve Çözümleri

### "Corel.Interop.VGCore.dll bulunamadı"
- CorelDRAW yüklü değil veya farklı bir yola kurulmuş
- Çözüm: DLL yolunu manuel belirtin (yukarıdaki #3'e bakın)

### "MSBuild bulunamadı"
- Visual Studio Build Tools kurulu değil
- Çözüm: Visual Studio 2022 Build Tools yükleyin veya .NET SDK kurun

### "Python bulunamadı"
- Python PATH'e eklenmemiş
- Çözüm: Python'u kurun ve PATH'e ekleyin veya tam yolu kullanın

## Sonraki Adımlar

Derleme başarılı olduktan sonra:

```powershell
.\scripts\package.ps1
```

Bu komut kurulum paketini oluşturur. `artifacts/installer` klasöründeki `.exe` dosyasını çalıştırarak StoneMaster'ı kurabilirsiniz.

## Yardım

Sorun yaşarsanız:
1. README.md dosyasını kontrol edin
2. docs/INSTALL.md dosyasına bakın
3. Hata mesajını kaydedip destek ekibiyle paylaşın
