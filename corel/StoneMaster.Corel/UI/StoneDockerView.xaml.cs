using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace StoneMaster.Corel.UI
{
    public partial class StoneDockerView : UserControl
    {
        private readonly StoneDockerViewModel _viewModel;
        private bool _busy;
        private readonly (string Name, string Hex)[] _palette =
        {
            ("Crystal", "#E8E8E8"), ("Black", "#111111"), ("White", "#FFFFFF"),
            ("Red", "#D2141E"), ("Blue", "#1446DC"), ("Green", "#1E9B4B"),
            ("Gold", "#D4AA2D"), ("Silver", "#BFC3C7"), ("Pink", "#E86A9D"),
            ("Orange", "#F28C28"), ("Purple", "#7948B9"), ("Brown", "#6C4931")
        };

        public StoneDockerView()
        {
            InitializeComponent();
            _viewModel = new StoneDockerViewModel(MainPlugin.Corel, MainPlugin.Engine, MainPlugin.Settings);
            DataContext = _viewModel;
            ModeBox.SelectedIndex = 0;
            BackgroundModeBox.SelectedIndex = 0;
            foreach (var color in _palette)
            {
                PalettePanel.Children.Add(new CheckBox
                {
                    Content = color.Name,
                    Tag = color.Name,
                    IsChecked = color.Name == "Crystal" || color.Name == "Black" || color.Name == "White",
                    Margin = new Thickness(0, 0, 8, 4)
                });
            }
            UpdateDensityLabel();
            DensitySlider.ValueChanged += (_, __) => UpdateDensityLabel();
        }

        private void UpdateDensityLabel() => DensityLabel.Text = DensitySlider.Value.ToString("0.00", CultureInfo.InvariantCulture);

        private void UseSelection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var context = MainPlugin.Corel.GetSelectedBitmapContext();
                _viewModel.SetBitmapContext(context);
                ImagePathBox.Text = context.ImagePath;
                StatusText.Text = "CorelDRAW bitmap'i hazır.";
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Görseller|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|Tüm dosyalar|*.*",
                Multiselect = false
            };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var context = MainPlugin.Corel.ImportBitmapContext(dialog.FileName);
                _viewModel.SetBitmapContext(context);
                ImagePathBox.Text = context.ImagePath;
                StatusText.Text = "Görsel CorelDRAW'a aktarıldı.";
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void Mode_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (ModeBox.SelectedItem is ComboBoxItem item)
                _viewModel.Mode = item.Content.ToString();
        }

        private async void AnalyzeColors_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            try
            {
                EnsureImage();
                SetBusy(true, "Renkler analiz ediliyor...");
                var colors = await _viewModel.AnalyzeImageColorsAsync(_viewModel.ImagePath);
                StatusText.Text = colors.Count == 0
                    ? "Renk analizi sonuç üretmedi."
                    : "Baskın renkler: " + string.Join(", ", colors.Take(6).Select(c => $"{c.Name} {c.Percentage:0.0}%"));
            }
            catch (Exception ex) { ShowError(ex); }
            finally { SetBusy(false); }
        }

        private async void Preview_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            try
            {
                ApplyUiToViewModel();
                EnsureImage();
                SetBusy(true, "Önizleme hesaplanıyor...");
                var response = await _viewModel.PreviewAsync(new Progress<string>(message => StatusText.Text = message));
                StatusText.Text = $"Önizleme hazır · {response.stone_count:N0} taş · {response.total_cost_tl:N2} TL · {response.used_colors} renk";
            }
            catch (Exception ex) { ShowError(ex); }
            finally { SetBusy(false); }
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            try
            {
                ApplyUiToViewModel();
                EnsureImage();
                if (_viewModel.LastResponse == null)
                    throw new InvalidOperationException("Önce Önizleme Oluştur ile sonucu doğrulayın.");

                MainPlugin.Corel.ApplyGeneration(_viewModel.LastResponse, _viewModel.CurrentBitmapContext, true, true, true);
                StatusText.Text = "CorelDRAW'a STONE_PREVIEW ve LAZER_KALIP_KESIM katmanları aktarıldı.";
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ApplyUiToViewModel()
        {
            var selectedSizes = StoneSizeList.Items
                .OfType<ListBoxItem>()
                .Select(item => item.Content as CheckBox)
                .Where(check => check?.IsChecked == true)
                .Select(check => check.Tag.ToString())
                .ToList();
            if (selectedSizes.Count == 0)
                selectedSizes.Add("SS10");

            _viewModel.StoneSizes = selectedSizes;
            _viewModel.StoneSize = selectedSizes[0];
            _viewModel.Density = DensitySlider.Value;
            _viewModel.Gap = ParseDouble(GapBox.Text, "Gap");
            _viewModel.LaserTolerance = ParseDouble(LaserToleranceBox.Text, "Lazer toleransı");
            _viewModel.EdgeSensitivity = EdgeSlider.Value;
            _viewModel.DetailSensitivity = DetailSlider.Value;
            _viewModel.BackgroundMode = (BackgroundModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "AUTO";
            _viewModel.BackgroundTolerance = ParseDouble(BackgroundToleranceBox.Text, "Arka plan toleransı");
            _viewModel.GridSnap = GridSnapBox.IsChecked == true;
            _viewModel.ExcludeDarkStones = ExcludeDarkBox.IsChecked == true;
            _viewModel.EdgeOnly = EdgeOnlyBox.IsChecked == true;
            _viewModel.Sprinkle = SprinkleBox.IsChecked == true;
            _viewModel.FabricWidthMm = ParseDouble(WidthBox.Text, "Genişlik");
            _viewModel.FabricHeightMm = string.IsNullOrWhiteSpace(HeightBox.Text) ? (double?)null : ParseDouble(HeightBox.Text, "Yükseklik");
            _viewModel.BudgetTl = ParseDouble(BudgetBox.Text, "Hedef bütçe");
            _viewModel.StoneUnitPriceTl = string.IsNullOrWhiteSpace(UnitPriceBox.Text) ? (double?)null : ParseDouble(UnitPriceBox.Text, "Birim fiyat");
            _viewModel.PaletteColors = PalettePanel.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => c.Tag.ToString()).ToList();
        }

        private static double ParseDouble(string text, string field)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0) return value;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && value >= 0) return value;
            throw new InvalidOperationException($"{field} değeri geçerli değil.");
        }

        private void EnsureImage()
        {
            if (string.IsNullOrWhiteSpace(_viewModel.ImagePath)) UseSelection_Click(this, new RoutedEventArgs());
            if (string.IsNullOrWhiteSpace(_viewModel.ImagePath)) throw new InvalidOperationException("Önce bir bitmap seçin veya görsel içe aktarın.");
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            ProgressBar.IsIndeterminate = busy;
            StatusText.Text = status;
        }

        private static void ShowError(Exception ex) => MessageBox.Show(ex.Message, "StoneMaster", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
