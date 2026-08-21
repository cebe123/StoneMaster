# StoneMaster for CorelDRAW

**StoneMaster – Rhinestone & Trok Designer**  
Kod adı: `StoneMaster.Corel`  
Sürüm: `1.0.0`

## Amaç

StoneMaster, CorelDRAW içinde dock edilebilir bir WPF Docker üzerinden kumaş/desen görsellerini analiz ederek rhinestone/trok yerleşim haritası üretir. Görüntü işleme motoru ayrı bir Python sürecinde çalışır; kullanıcı Python, CSV veya VBA ile uğraşmaz.

## Önemli mimari kararı

CorelDRAW custom Docker tarafı **.NET Framework 4.8 + WPF** hedefler. Modern `.NET 8` WPF Docker doğrudan CorelDRAW içinde çalıştırılmamalıdır; güncel CorelDRAW SDK topluluk dokümantasyonunda WPF/.NET 8 yerine eski .NET Framework yaklaşımı önerilmektedir. Modern iş mantığı gerekirse Docker'dan bağımsız bir .NET Standard/.NET 8 kütüphanesine taşınabilir.

CorelDRAW 2024/v25, 2025/v26 ve 2026/v27 API dokümantasyonlarında `FrameWork.AddDocker(Guid, ClassName, AssemblyPath)` mevcuttur.

## Dizin

```text
StoneMaster/
  corel/StoneMaster.Corel/      WPF Docker + CorelDRAW automation
  engine/stone_engine/           OpenCV/Numpy görüntü işleme motoru
  engine/tests/                  Python testleri
  config/                        Taş/palet/default ayarları
  installer/                     Inno Setup
  scripts/                       build/publish scriptleri
  docs/                          kurulum/kullanım/development
```

## Derleme gereksinimleri

### Geliştirme makinesi

- Windows 10/11 x64
- CorelDRAW Graphics Suite 2024/2025/2026
- Visual Studio 2022
- .NET Framework 4.8 Developer Pack
- Python 3.x (yalnızca build makinesi)
- PyInstaller
- Inno Setup 6
- CorelDRAW SDK/Interop DLLs: geliştirme makinesindeki CorelDRAW kurulumundan erişilir

> `Corel.Interop.VGCore.dll` burada lisanslı Corel kurulumundan gelmelidir; depo içine yeniden dağıtılmaz.

## CorelDRAW SDK uyumluluğu

Doğrulanan API noktaları:

- Custom Docker: `FrameWork.AddDocker(...)`
- Addon klasörü: `Programs64\Addons`
- WPF hosted Docker: `type="wpfhost"` + `hostedType="Addons\...\Assembly.dll,Namespace.DockerUI"`
- `Layer.CreateEllipse2(centerX, centerY, radiusX, radiusY)` merkez + yarıçap mantığı kullanır; taş çapı doğrudan verilmez.

Bu nedenle kodda `CreateEllipse2(x, y, diameter / 2)` kullanılır.

## Kurulum

1. `scripts\build.ps1` ile engine ve Corel Docker build edilir.
2. `installer\StoneMaster.iss` ile `StoneMaster_Setup.exe` oluşturulur.
3. Setup çalıştırılır.
4. Kurulum CorelDRAW'ın `Programs64\Addons\StoneMaster` klasörünü bulup add-on dosyalarını yerleştirir.
5. CorelDRAW yeniden başlatılır.
6. `Window > Dockers > StoneMaster` yolundan Docker açılır.

CorelDRAW workspace özelleştirmesinin yeniden yüklenmesi gereken ilk kurulumlarda F8 ile workspace reset gerekebilir. Bu, Corel'in resmi custom add-on dokümantasyonunda belirtilen bir çalışma davranışıdır.

## Kullanım

1. CorelDRAW'da bitmap seç.
2. StoneMaster Docker'ını aç.
3. `Seçili Bitmap Kullan` ile görseli al veya `Görsel Seç` ile doğrudan JPG/PNG seç.
4. Kumaş genişliğini gir.
5. Taş boyutu, renk paleti, gap, yoğunluk ve bütçe modunu ayarla.
6. `Önizleme`.
7. `CorelDRAW'a Uygula`.
8. Üretim sonucu:
   - `ORIGINAL_FABRIC`
   - `STONE_PREVIEW`
   - `LAZER_KALIP_KESIM`
   layer'larına yazılır.

## Üretim notları

- 10.000+ taş için batch/command group kullanılır.
- 100.000+ taş büyük bir CorelDRAW dokümanında hâlâ ağır olabilir; bu nedenle raster preview ile vector apply ayrılmıştır.
- Lazer kalıp kırmızı hairline + no-fill olarak hazırlanır.
- `CreateEllipse2` fiziksel çap değil yarıçap kabul ettiği için `diameter_mm / 2` kullanılır.
- JSON IPC kullanılır; CSV yalnızca dışa aktarım içindir.

## Bilinen sınırlama

Bu paket kaynak kodu ve installer scriptini içerir; **CorelDRAW'ın lisanslı `VGCore.dll` dosyası ve derlenmiş Corel SDK interop assembly'si** dağıtıma dahil edilmez. Son `.exe` build'i CorelDRAW yüklü Windows geliştirme makinesinde yapılmalıdır.
