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

        public StoneDockerViewModel(CorelDrawService corel, PythonEngineService engine, SettingsService settings)
        {
            _corel = corel ?? throw new ArgumentNullException(nameof(corel));
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            StoneSize = "SS10";
            StoneSizes = new List<string> { "SS10" };
            Gap = 0.5;
            Density = 0.65;
            BackgroundThreshold = 245;
            BackgroundTolerance = 28;
            BackgroundMode = "LIGHT";
            BackgroundColor = "#F5F5F5";
            LaserTolerance = 0.3;
            EdgeSensitivity = 0.5;
            DetailSensitivity = 0.5;
            Mode = "FULL";
            BudgetTl = 200;
            DarkStoneThreshold = 70;
            EdgeThreshold = 80;
            AnalysisMaxDimension = 1600;
        }

        public BitmapContext CurrentBitmapContext { get; private set; }
        public string ImagePath { get; private set; }
        public string StoneSize { get; set; }
        public List<string> StoneSizes { get; set; }
        public List<string> PaletteColors { get; set; } = new List<string>();
        public List<string> CustomPaletteHex { get; set; } = new List<string>();
        public bool Sprinkle { get; set; }
        public bool ExcludeDarkStones { get; set; }
        public int DarkStoneThreshold { get; set; }
        public List<List<string>> ExcludedStoneGroups { get; } = new List<List<string>>();
        public bool EdgeOnly { get; set; }
        public int EdgeThreshold { get; set; }
        public bool GridSnap { get; set; }
        public List<(int x, int y)> InteractivePoints { get; set; } = new List<(int, int)>();
        public double Gap { get; set; }
        public double Density { get; set; }
        public int BackgroundThreshold { get; set; }
        public string BackgroundMode { get; set; }
        public double BackgroundTolerance { get; set; }
        public string BackgroundColor { get; set; }
        public double LaserTolerance { get; set; }
        public double EdgeSensitivity { get; set; }
        public double DetailSensitivity { get; set; }
        public double? FabricWidthMm { get; set; }
        public double? FabricHeightMm { get; set; }
        public string Mode { get; set; }
        public double BudgetTl { get; set; }
        public double? StoneUnitPriceTl { get; set; }
        public int AnalysisMaxDimension { get; set; }
        public string Status { get; private set; }
        public EngineResponse LastResponse { get; private set; }

        public async Task<EngineResponse> PreviewAsync(IProgress<string> progress)
        {
            var path = ImagePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                var context = _corel.GetSelectedBitmapContext();
                SetBitmapContext(context);
                path = ImagePath;
            }

            var request = BuildRequest(path);
            Cancel();
            _cts = new CancellationTokenSource();
            Status = "İşleniyor...";
            OnPropertyChanged(nameof(Status));

            try
            {
                var response = await _engine.GenerateAsync(request, progress, _cts.Token).ConfigureAwait(true);
                LastResponse = response;
                Status = $"Hazır: {response.stone_count:N0} taş / {response.total_cost_tl:N2} TL";
                OnPropertyChanged(nameof(Status));
                return response;
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        public void SetImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Görsel yolu boş olamaz.", nameof(path));
            ImagePath = path;
            OnPropertyChanged(nameof(ImagePath));
        }

        public void SetBitmapContext(BitmapContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.ImagePath))
                throw new ArgumentException("Geçerli bir bitmap context gerekli.", nameof(context));
            CurrentBitmapContext = context;
            ImagePath = context.ImagePath;
            OnPropertyChanged(nameof(CurrentBitmapContext));
            OnPropertyChanged(nameof(ImagePath));
        }

        public void Cancel()
        {
            try { _cts?.Cancel(); } catch { }
        }

        public void SetExclusionRect(List<double> rect)
        {
            if (rect != null && (rect.Count != 4 || rect.Any(value => value < 0 || value > 1)))
                throw new ArgumentException("ExclusionRect [x,y,w,h] 0..1 aralığında olmalıdır.", nameof(rect));
            ExclusionRect = rect;
        }

        public List<double> ExclusionRect { get; private set; }

        public void ExcludeStoneGroup(string stoneName, string colorName)
        {
            if (string.IsNullOrWhiteSpace(stoneName) || string.IsNullOrWhiteSpace(colorName)) return;
            if (!ExcludedStoneGroups.Any(item => item.Count >= 2 &&
                string.Equals(item[0], stoneName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item[1], colorName, StringComparison.OrdinalIgnoreCase)))
            {
                ExcludedStoneGroups.Add(new List<string> { stoneName, colorName });
            }
        }

        private EngineRequest BuildRequest(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("İşlenecek görsel bulunamadı.");

            var mode = (Mode ?? "FULL").Trim().ToUpperInvariant();
            var style = mode == "EDGE" ? "edge"
                : mode == "FILL" ? "fill"
                : mode == "SCATTER" ? "scatter"
                : "balanced";

            return new EngineRequest
            {
                image_path = path,
                width_mm = FabricWidthMm,
                height_mm = FabricHeightMm,
                stone_size = StoneSize,
                stone_sizes = (StoneSizes == null || StoneSizes.Count == 0) ? new List<string> { StoneSize ?? "SS10" } : StoneSizes,
                palette_colors = PaletteColors,
                gap_mm = Gap,
                laser_tolerance_mm = LaserTolerance,
                density = Math.Max(0.01, Math.Min(1.0, Density)),
                background_threshold = BackgroundThreshold,
                background_mode = BackgroundMode,
                background_tolerance = BackgroundTolerance,
                background_color = BackgroundColor,
                edge_sensitivity = EdgeSensitivity,
                detail_sensitivity = DetailSensitivity,
                mode = mode,
                style = style,
                calculate_cost = true,
                budget_enabled = mode == "BUDGET",
                target_budget_tl = BudgetTl,
                stone_unit_price_tl = StoneUnitPriceTl,
                exclusion_rect = ExclusionRect,
                custom_palette_hex = CustomPaletteHex,
                sprinkle = Sprinkle || mode == "SCATTER",
                exclude_dark_stones = ExcludeDarkStones,
                dark_stone_threshold = DarkStoneThreshold,
                excluded_stone_groups = ExcludedStoneGroups,
                edge_only = EdgeOnly || mode == "EDGE",
                edge_threshold = EdgeThreshold,
                grid_snap = GridSnap,
                interactive_points = InteractivePoints,
                analysis_max_dimension = AnalysisMaxDimension
            };
        }

        public async Task<List<(string Name, string Hex, double Percentage)>> AnalyzeImageColorsAsync(string imagePath)
        {
            return await _engine.AnalyzeImageColorsAsync(imagePath).ConfigureAwait(true);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
