using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using StoneMaster.Corel.Corel;
using StoneMaster.Corel.Models;
using StoneMaster.Corel.Services;

namespace StoneMaster.Corel.UI
{
    public sealed class StoneDockerViewModel : INotifyPropertyChanged
    {
        private readonly CorelDrawService _corel;
        private readonly PythonEngineService _engine;
        private readonly SettingsService _settings;
        private CancellationTokenSource _cts;
        public BitmapContext CurrentBitmapContext { get; private set; }

        public StoneDockerViewModel(CorelDrawService corel, PythonEngineService engine, SettingsService settings)
        {
            _corel = corel;
            _engine = engine;
            _settings = settings;
            StoneSize = "SS10";
            Gap = 0.5;
            Density = 0.65;
            BackgroundThreshold = 245;
            LaserTolerance = 0.3;
            EdgeSensitivity = 0.5;
            DetailSensitivity = 0.5;
            Mode = "FULL";
        }

        public string ImagePath { get; set; }
        public string StoneSize { get; set; }
        public List<string> StoneSizes { get; set; } = new List<string> { "SS10" };
        public List<string> PaletteColors { get; set; }
        public List<string> CustomPaletteHex { get; set; } = new List<string>();
        public bool Sprinkle { get; set; }
        public bool ExcludeDarkStones { get; set; }
        public int DarkStoneThreshold { get; set; } = 70;
        public List<List<string>> ExcludedStoneGroups { get; } = new List<List<string>>();
        public bool EdgeOnly { get; set; }
        public int EdgeThreshold { get; set; } = 80;
        public bool GridSnap { get; set; }
        public List<(int x, int y)> InteractivePoints { get; set; } = new List<(int, int)>();
        public double Gap { get; set; }
        public double Density { get; set; }
        public int BackgroundThreshold { get; set; }
        public string BackgroundMode { get; set; } = "LIGHT";
        public double BackgroundTolerance { get; set; } = 28;
        public string BackgroundColor { get; set; } = "#F5F5F5";
        public double LaserTolerance { get; set; }
        public double EdgeSensitivity { get; set; }
        public double DetailSensitivity { get; set; }
        public double? FabricWidthMm { get; set; }
        public double? FabricHeightMm { get; set; }
        public string Mode { get; set; }
        public double BudgetTl { get; set; } = 200;
        public double? StoneUnitPriceTl { get; set; }
        public string Status { get; private set; }
        public EngineResponse LastResponse { get; private set; }

        public async Task<EngineResponse> PreviewAsync(IProgress<string> progress)
        {
            EngineRequest request;

            if (!string.IsNullOrWhiteSpace(ImagePath))
            {
                request = BuildRequest(ImagePath);
            }
            else
            {
                var context = _corel.GetSelectedBitmapContext();
                CurrentBitmapContext = context;
                request = BuildRequest(context.ImagePath);
            }

            _cts = new CancellationTokenSource();
            Status = "Processing...";
            OnPropertyChanged(nameof(Status));

            LastResponse = await _engine.GenerateAsync(request, progress, _cts.Token);
            if (!LastResponse.success)
                throw new InvalidOperationException(LastResponse.error);

            Status = $"Hazır: {LastResponse.stone_count:N0} taş / {LastResponse.total_cost_tl:N2} TL";
            OnPropertyChanged(nameof(Status));
            return LastResponse;
        }

        public void SetImagePath(string path)
        {
            ImagePath = path;
            CurrentBitmapContext = new BitmapContext { ImagePath = path };
        }

        public void SetBitmapContext(BitmapContext context)
        {
            CurrentBitmapContext = context;
            ImagePath = context?.ImagePath;
        }

        public void Cancel() => _cts?.Cancel();

        private EngineRequest BuildRequest(string path)
        {
            // Stil belirleme: Mode parametresini style olarak kullan
            // FULL -> balanced, EDGE -> edge, FILL -> fill, SCATTER -> scatter
            // ÖNEMLİ: edge_only checkbox'ı kaldırıldı, sadece mode kullanılıyor
            string style = "balanced";
            string modeUpper = (Mode ?? "FULL").ToUpper();
            
            if (modeUpper == "EDGE")
                style = "edge";
            else if (modeUpper == "FILL")
                style = "fill";
            else if (modeUpper == "SCATTER" || Sprinkle)
                style = "scatter";
            else if (modeUpper == "BUDGET")
                style = "balanced"; // Bütçe modu balanced stil ile çalışır
            else
                style = "balanced"; // FULL varsayılan
            
            // Otomatik yoğunluk ayarı (mod bazlı)
            double autoDensity = Density;
            if (modeUpper == "FULL")
                autoDensity = Math.Max(Density, 0.7); // Tam doldurma için yüksek yoğunluk
            else if (modeUpper == "EDGE")
                autoDensity = Math.Min(Density, 0.5); // Kenar için düşük yoğunluk
            else if (modeUpper == "FILL")
                autoDensity = Math.Max(Density, 0.6); // İç doldurma için orta-yüksek yoğunluk
            else if (modeUpper == "SCATTER")
                autoDensity = Math.Min(Density, 0.4); // Serpme için düşük yoğunluk
            
            return new EngineRequest
            {
                image_path = path,
                width_mm = FabricWidthMm,
                height_mm = FabricHeightMm,
                stone_size = StoneSize,
                stone_sizes = StoneSizes,
                palette_colors = PaletteColors,
                gap_mm = Gap,
                laser_tolerance_mm = LaserTolerance,
                density = autoDensity,
                background_threshold = BackgroundThreshold,
                background_mode = BackgroundMode,
                background_tolerance = BackgroundTolerance,
                background_color = BackgroundColor,
                edge_sensitivity = EdgeSensitivity,
                detail_sensitivity = DetailSensitivity,
                mode = Mode,
                style = style,
                calculate_cost = true,
                budget_enabled = modeUpper == "BUDGET",
                target_budget_tl = BudgetTl,
                stone_unit_price_tl = StoneUnitPriceTl,
                exclusion_rect = ExclusionRect,
                custom_palette_hex = CustomPaletteHex,
                sprinkle = Sprinkle,
                exclude_dark_stones = ExcludeDarkStones,
                dark_stone_threshold = DarkStoneThreshold,
                excluded_stone_groups = ExcludedStoneGroups,
                edge_only = EdgeOnly, // Artık checkbox ile kontrol ediliyor
                edge_threshold = EdgeThreshold,
                grid_snap = GridSnap,
                interactive_points = InteractivePoints
            };
        }

        public void ExcludeStoneGroup(string stoneName, string colorName)
        {
            if (!ExcludedStoneGroups.Any(item => item.Count == 2 && item[0] == stoneName && item[1] == colorName))
                ExcludedStoneGroups.Add(new List<string> { stoneName, colorName });
        }

        public List<double> ExclusionRect { get; private set; }

        public void SetExclusionRect(List<double> rect)
        {
            ExclusionRect = rect;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public async Task<List<(string Name, string Hex, double Percentage)>> AnalyzeImageColorsAsync(string imagePath)
        {
            return await _engine.AnalyzeImageColorsAsync(imagePath);
        }
    }
}