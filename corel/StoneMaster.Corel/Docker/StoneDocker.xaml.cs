using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using StoneMaster.Corel.UI;

namespace StoneMaster.Corel.Docker
{
    public partial class StoneDocker
    {
        private readonly HashSet<int> _selectedStoneIndexes = new HashSet<int>();
        private bool _sampleColorMode;
        private bool _excludeSelectedMode;
        private bool _interactiveSelectionMode;
        private bool _invertSelection;
        private bool _previewEdited;
        private List<(int x, int y)> _interactivePoints = new List<(int, int)>();
        private StoneDockerViewModel Vm => (StoneDockerViewModel)DataContext;

        private void OpenFloatingWindow_Click(object sender, RoutedEventArgs e)
        {
            StoneFloatingWindow.Open(MainPlugin.Corel.Application);
        }

        private void UseSelected_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ctx = MainPlugin.Corel.GetSelectedBitmapContext();
                Vm.SetImagePath(ctx.ImagePath);
                txtImage.Text = "CorelDRAW bitmap (seçili)";
                txtStatus.Text = "CorelDRAW bitmap seçildi.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "StoneMaster", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExcludeSelectedArea_Click(object sender, RoutedEventArgs e)
        {
            _excludeSelectedMode = true;
            _selectedStoneIndexes.Clear();
            txtStatus.Text = "Önizlemede taşsız bırakılacak taş grubuna tıklayın, sonra Seçili taşları sil'e basın.";
        }

        private void SelectImage_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png|All Files|*.*",
                CheckFileExists = true,
                Multiselect = false,
                Title = "StoneMaster için görsel seçin"
            })
            {
                var corelWindow = Process.GetCurrentProcess().MainWindowHandle;
                var result = corelWindow == IntPtr.Zero
                    ? dialog.ShowDialog()
                    : dialog.ShowDialog(new CorelWindow(corelWindow));

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        var context = MainPlugin.Corel.ImportBitmapContext(dialog.FileName);
                        Vm.SetBitmapContext(context);
                        txtImage.Text = System.IO.Path.GetFileName(Vm.ImagePath);
                        txtStatus.Text = "Görsel CorelDRAW çalışma alanına aktarıldı.";
                    }
                    catch (Exception ex)
                    {
                        txtStatus.Text = "Görsel aktarılamadı: " + ex.Message;
                        MessageBox.Show(ex.Message, "StoneMaster", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        private sealed class CorelWindow : System.Windows.Forms.IWin32Window
        {
            public CorelWindow(IntPtr handle) => Handle = handle;
            public IntPtr Handle { get; }
        }

        private async void Preview_Click(object sender, RoutedEventArgs e)
        {
            ReadUi();
            
            // İnteraktif seçim modunda nokta listesini engine'e geçir
            if (_interactiveSelectionMode && _interactivePoints.Count > 0)
            {
                txtStatus.Text = $"🎯 {_interactivePoints.Count} nokta seçildi. Önizleme oluşturuluyor...";
            }
            
            try
            {
                SetProgress(0, "Hazırlanıyor...");
                var response = await Vm.PreviewAsync(new Progress<string>(UpdateProgress));
                RenderPreview(response);
                _previewEdited = false;
                
                // İnteraktif mod işaretçilerini temizle
                if (_interactiveSelectionMode)
                {
                    _interactiveSelectionMode = false;
                    txtSelectionMode.Text = "";
                }
                
                SetProgress(100, "Tamamlandı");
                
                // Durum bilgisi güncelle
                txtStoneCount.Text = $"🔢 Taş Sayısı: {response.stone_count:N0}";
                txtCost.Text = $"💰 Tahmini Maliyet: {response.total_cost_tl:N2} TL";
                txtStatus.Text = $"✅ Hazır: {response.stone_count:N0} taş | {response.total_cost_tl:N2} TL | {response.used_colors} renk";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "❌ Hata: " + ex.Message;
                MessageBox.Show(ex.Message, "StoneMaster", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Apply_Click(object sender, RoutedEventArgs e)
        {
            ReadUi();
            try
            {
                SetProgress(0, "Hazırlanıyor...");
                var response = Vm.LastResponse;
                if (response == null || !_previewEdited)
                    response = await Vm.PreviewAsync(new Progress<string>(UpdateProgress));
                response = FilterResponse(response);
                
                // CorelDRAW'a aktar - görsel boyutları doğru şekilde geçiriliyor
                MainPlugin.Corel.ApplyGeneration(response, Vm.CurrentBitmapContext, renderPreview: true, renderLaser: true, clearPrevious: true);
                
                SetProgress(100, "Tamamlandı");
                txtStoneCount.Text = $"Taş sayısı: {response.stones.Count:N0}";
                
                // Maliyet bilgisi güncelle
                if (Vm.LastResponse != null)
                {
                    txtCost.Text = $"💰 Tahmini Maliyet: {Vm.LastResponse.total_cost_tl:N2} TL";
                }
                
                txtStatus.Text = $"✅ Uygulandı: {response.stone_count:N0} taş, {response.total_cost_tl:N2} TL";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "❌ Hata: " + ex.Message;
                MessageBox.Show(ex.Message, "StoneMaster", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void RenderPreview(StoneMaster.Corel.Models.EngineResponse response)
        {
            PreviewCanvas.Children.Clear();
            txtStoneCount.Text = $"Taş sayısı: {response?.stones?.Count ?? 0:N0}";
            if (response == null || response.width_mm <= 0 || response.height_mm <= 0) return;

            const double previewWidth = 360.0;
            var previewHeight = previewWidth * response.height_mm / response.width_mm;
            PreviewCanvas.Width = previewWidth;
            PreviewCanvas.Height = previewHeight;
            PreviewSurface.Width = previewWidth;
            PreviewSurface.Height = previewHeight;

            PreviewImage.Source = null;
            if (!string.IsNullOrWhiteSpace(Vm.ImagePath) && File.Exists(Vm.ImagePath))
            {
                var image = new System.Windows.Media.Imaging.BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(Vm.ImagePath, UriKind.Absolute);
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.EndInit();
                PreviewImage.Source = image;
                PreviewImage.Visibility = chkShowBackground.IsChecked.GetValueOrDefault()
                    ? Visibility.Visible : Visibility.Collapsed;
            }

            var scaleX = PreviewCanvas.Width / response.width_mm;
            var scaleY = PreviewCanvas.Height / response.height_mm;
            var stoneScale = System.Math.Min(scaleX, scaleY);

            for (var index = 0; index < response.stones.Count; index++)
            {
                var stone = response.stones[index];
                var d = System.Math.Max(2.0, stone.DiameterMm * stoneScale);
                var brush = TryCreateBrush(stone.HexColor);
                var ellipse = new Ellipse
                {
                    Width = d,
                    Height = d,
                    Fill = brush,
                    Stroke = _selectedStoneIndexes.Contains(index) ? System.Windows.Media.Brushes.Yellow : System.Windows.Media.Brushes.Transparent,
                    StrokeThickness = _selectedStoneIndexes.Contains(index) ? 2 : 0
                };
                ellipse.Tag = index;
                ellipse.MouseLeftButtonDown += PreviewStone_MouseLeftButtonDown;
                Canvas.SetLeft(ellipse, stone.XMm * scaleX - d / 2);
                Canvas.SetTop(ellipse, stone.YMm * scaleY - d / 2);
                PreviewCanvas.Children.Add(ellipse);
            }
        }

        private void PreviewStone_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_sampleColorMode)
                return;

            var ellipse = (Ellipse)sender;
            var index = (int)ellipse.Tag;
            var clickedStone = Vm.LastResponse.stones[index];
            var matchingIndexes = Vm.LastResponse.stones
                .Select((stone, itemIndex) => new { stone, itemIndex })
                .Where(item => item.stone.StoneName == clickedStone.StoneName
                    && item.stone.ColorName == clickedStone.ColorName)
                .Select(item => item.itemIndex);
            var selectGroup = !_selectedStoneIndexes.Contains(index);
            foreach (var matchingIndex in matchingIndexes)
            {
                if (selectGroup)
                    _selectedStoneIndexes.Add(matchingIndex);
                else
                    _selectedStoneIndexes.Remove(matchingIndex);
            }
            if (_excludeSelectedMode)
            {
                _excludeSelectedMode = false;
                txtStatus.Text = "Taşsız bırakılacak grup seçildi. Tümünü kaldırmak için Seçili taşları sil'e basın.";
            }
            RenderPreview(Vm.LastResponse);
            e.Handled = true;
        }

        private void DeleteSelectedStones_Click(object sender, RoutedEventArgs e)
        {
            if (Vm.LastResponse == null || _selectedStoneIndexes.Count == 0)
                return;

            var deletedGroups = Vm.LastResponse.stones
                .Where((_, index) => _selectedStoneIndexes.Contains(index))
                .Select(item => new { item.StoneName, item.ColorName })
                .Distinct()
                .ToList();
            foreach (var group in deletedGroups)
                Vm.ExcludeStoneGroup(group.StoneName, group.ColorName);
            Vm.LastResponse.stones = Vm.LastResponse.stones
                .Where((_, index) => !_selectedStoneIndexes.Contains(index))
                .ToList();
            Vm.LastResponse.stone_count = Vm.LastResponse.stones.Count;
            txtStoneCount.Text = $"Taş sayısı: {Vm.LastResponse.stones.Count:N0}";
            _selectedStoneIndexes.Clear();
            _excludeSelectedMode = false;
            _previewEdited = true;
            RenderPreview(Vm.LastResponse);
            txtStatus.Text = "Seçili taşlar silindi. Değişiklikleri Corel'e aktarmak için Uygula'ya basın.";
        }

        private void SampleColor_Click(object sender, RoutedEventArgs e)
        {
            _sampleColorMode = true;
            txtStatus.Text = "Önizleme üzerindeki bir noktaya tıklayarak rengi ekleyin.";
        }

        private void SelectBackgroundColor_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.ColorDialog())
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                var color = dialog.Color;
                Vm.BackgroundColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                txtBackgroundColor.Text = Vm.BackgroundColor;
                cmbBackgroundMode.SelectedIndex = 3; // COLOR modunu seç
                
                // Arkaplan rengi seçildiğinde analiz panelini göster
                pnlColorAnalysis.Visibility = Visibility.Visible;
                pnlPalette.Visibility = Visibility.Collapsed;
                
                txtStatus.Text = $"Arkaplan rengi seçildi: {Vm.BackgroundColor}. Şimdi 'Fotoğrafı Analiz Et' butonuna tıklayın.";
            }
        }

        private async void AnalyzeImage_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Vm.ImagePath) || !File.Exists(Vm.ImagePath))
            {
                MessageBox.Show("Lütfen önce bir fotoğraf seçin.", "StoneMaster", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                txtAnalysisStatus.Text = "🔍 Fotoğraf analiz ediliyor...";
                cmbAnalyzedColors.Items.Clear();
                
                // Python engine ile renk analizi yap
                var analyzedColors = await Vm.AnalyzeImageColorsAsync(Vm.ImagePath);
                
                if (analyzedColors.Count == 0)
                {
                    txtAnalysisStatus.Text = "⚠️ Renk analizi yapılamadı.";
                    return;
                }
                
                // Her rengi ListBox'a ekle
                foreach (var colorInfo in analyzedColors)
                {
                    var checkBox = new CheckBox
                    {
                        Content = $"{colorInfo.Name} ({colorInfo.Hex}) - %{colorInfo.Percentage:F1}",
                        IsChecked = true // Varsayılan olarak tümünü seç
                    };
                    
                    // Arkaplan rengine yakın olanları otomatik işaretleme
                    if (!string.IsNullOrEmpty(Vm.BackgroundColor))
                    {
                        var bgColor = System.Drawing.ColorTranslator.FromHtml(Vm.BackgroundColor);
                        var thisColor = System.Drawing.ColorTranslator.FromHtml(colorInfo.Hex);
                        int diff = Math.Abs(thisColor.R - bgColor.R) + 
                                   Math.Abs(thisColor.G - bgColor.G) + 
                                   Math.Abs(thisColor.B - bgColor.B);
                        
                        if (diff < 90) // Arkaplan rengine çok yakın, işaretleme
                        {
                            checkBox.IsChecked = false;
                        }
                    }
                    
                    cmbAnalyzedColors.Items.Add(new ListBoxItem { Content = checkBox });
                }
                
                txtAnalysisStatus.Text = $"✅ {analyzedColors.Count} renk bulundu. Taş olarak kullanılacak renkleri seçin ve 'ÖNİZLEME OLUŞTUR'a basın.";
            }
            catch (Exception ex)
            {
                txtAnalysisStatus.Text = "❌ Hata: " + ex.Message;
                MessageBox.Show(ex.Message, "StoneMaster", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreviewCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // İnteraktif alan seçimi modu
            if (_interactiveSelectionMode)
            {
                var point = e.GetPosition(PreviewCanvas);
                // Preview canvas koordinatlarını orijinal görsel koordinatlarına çevir
                if (Vm.LastResponse != null && !string.IsNullOrWhiteSpace(Vm.ImagePath))
                {
                    using (var bitmap = new Bitmap(Vm.ImagePath))
                    {
                        var imgX = (int)(point.X / PreviewCanvas.Width * bitmap.Width);
                        var imgY = (int)(point.Y / PreviewCanvas.Height * bitmap.Height);
                        imgX = Math.Max(0, Math.Min(bitmap.Width - 1, imgX));
                        imgY = Math.Max(0, Math.Min(bitmap.Height - 1, imgY));
                        
                        _interactivePoints.Add((imgX, imgY));
                        
                        // Görsel geri bildirim - küçük bir daire çiz
                        var marker = new Ellipse
                        {
                            Width = 8,
                            Height = 8,
                            Fill = System.Windows.Media.Brushes.Red,
                            Stroke = System.Windows.Media.Brushes.White,
                            StrokeThickness = 1
                        };
                        Canvas.SetLeft(marker, point.X - 4);
                        Canvas.SetTop(marker, point.Y - 4);
                        PreviewCanvas.Children.Add(marker);
                        
                        txtSelectionMode.Text = $"🎯 {_interactivePoints.Count} nokta seçildi";
                    }
                }
                e.Handled = true;
                return;
            }
            
            // Renk örnekleme modu
            if (!_sampleColorMode || string.IsNullOrWhiteSpace(Vm.ImagePath) || !File.Exists(Vm.ImagePath))
                return;

            var clickPoint = e.GetPosition(PreviewCanvas);
            using (var bitmap = new Bitmap(Vm.ImagePath))
            {
                var x = Math.Max(0, Math.Min(bitmap.Width - 1, (int)(clickPoint.X / PreviewCanvas.Width * bitmap.Width)));
                var y = Math.Max(0, Math.Min(bitmap.Height - 1, (int)(clickPoint.Y / PreviewCanvas.Height * bitmap.Height)));
                var color = bitmap.GetPixel(x, y);
                var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                
                // Arka plan rengine yakın renkleri kontrol et
                var isBackgroundColor = false;
                if (!string.IsNullOrEmpty(Vm.BackgroundColor))
                {
                    var bgColor = System.Drawing.ColorTranslator.FromHtml(Vm.BackgroundColor);
                    var clickedColor = color;
                    int diff = Math.Abs(clickedColor.R - bgColor.R) + 
                               Math.Abs(clickedColor.G - bgColor.G) + 
                               Math.Abs(clickedColor.B - bgColor.B);
                    if (diff < 90) // Tolerans
                    {
                        isBackgroundColor = true;
                    }
                }
                
                // Hariç tutma modunda veya arka plan rengi seçilmişse
                if (_excludeSelectedMode || isBackgroundColor)
                {
                    // Bu rengi paletten çıkar
                    for (int i = cmbPalette.Items.Count - 1; i >= 0; i--)
                    {
                        var item = cmbPalette.Items[i] as ListBoxItem;
                        var checkBox = item?.Content as CheckBox;
                        if (checkBox != null && checkBox.Content.ToString().Contains(hex))
                        {
                            cmbPalette.Items.RemoveAt(i);
                        }
                    }
                    Vm.CustomPaletteHex.RemoveAll(h => h == hex);
                    
                    if (isBackgroundColor)
                    {
                        txtStatus.Text = $"Arka plan rengi ({hex}) otomatik olarak hariç tutuldu.";
                    }
                    else
                    {
                        txtStatus.Text = $"Renk hariç tutuldu: {hex}";
                    }
                }
                else
                {
                    // Normal renk ekleme modu
                    if (!Vm.CustomPaletteHex.Contains(hex))
                    {
                        Vm.CustomPaletteHex.Add(hex);
                        cmbPalette.Items.Add(new ListBoxItem
                        {
                            Content = new CheckBox { Content = hex, IsChecked = true }
                        });
                        txtStatus.Text = $"Renk eklendi: {hex}";
                    }
                    else
                    {
                        txtStatus.Text = $"Bu renk zaten seçili: {hex}";
                    }
                }
            }
            _sampleColorMode = false;
            _excludeSelectedMode = false;
            e.Handled = true;
        }

        private void BackgroundVisibility_Click(object sender, RoutedEventArgs e)
        {
            PreviewImage.Visibility = chkShowBackground.IsChecked.GetValueOrDefault()
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Zoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PreviewSurface != null)
            {
                PreviewSurface.LayoutTransform = new ScaleTransform(e.NewValue, e.NewValue);
            }
        }

        private static SolidColorBrush TryCreateBrush(string hexColor)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hexColor))
                    return (SolidColorBrush)new BrushConverter().ConvertFromString(hexColor);
            }
            catch (FormatException) { }

            return new SolidColorBrush(Colors.DimGray);
        }

        private StoneMaster.Corel.Models.EngineResponse FilterResponse(StoneMaster.Corel.Models.EngineResponse response)
        {
            if (!chkApplySelectedOnly.IsChecked.GetValueOrDefault())
                return response;

            var allowedSizes = Vm.StoneSizes ?? new System.Collections.Generic.List<string>();
            var allowedColors = Vm.PaletteColors ?? new System.Collections.Generic.List<string>();
            response.stones = response.stones.Where(stone =>
                allowedSizes.Contains(stone.StoneName) &&
                (allowedColors.Count == 0 || allowedColors.Contains(stone.ColorName) || allowedColors.Contains(stone.HexColor)))
                .ToList();
            response.stone_count = response.stones.Count;
            return response;
        }

        private void UpdateProgress(string message)
        {
            var parts = (message ?? string.Empty).Split('|');
            foreach (var part in parts)
            {
                if (part.StartsWith("PROGRESS=", StringComparison.OrdinalIgnoreCase))
                {
                    double value;
                    if (double.TryParse(part.Substring(9), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out value))
                        ProgressBar.Value = Math.Max(0, Math.Min(100, value));
                }
                else if (!part.StartsWith("STAGE=", StringComparison.OrdinalIgnoreCase))
                {
                    txtProgress.Text = part;
                }
            }
        }

        private void SetProgress(double value, string text)
        {
            ProgressBar.Value = value;
            txtProgress.Text = text;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Vm.Cancel();

        private void ReadUi()
        {
            Vm.StoneSizes = GetCheckedValues(cmbStoneSizes);
            if (Vm.StoneSizes.Count == 0)
                throw new InvalidOperationException("En az bir taş boyutunu işaretleyin.");
            
            // ÖNEMLİ: Eğer analiz edilmiş renkler varsa, sadece bunları kullan
            // Yoksa custom_palette_hex veya normal palet renklerini kullan
            var analyzedColors = GetCheckedAnalyzedColors();
            if (analyzedColors.Count > 0)
            {
                // Analiz edilen ve seçili renkleri kullan
                Vm.PaletteColors = new List<string>(); // Boş bırak, custom_palette_hex kullanılacak
                Vm.CustomPaletteHex = analyzedColors.Select(c => c.Hex).ToList();
            }
            else if (Vm.CustomPaletteHex != null && Vm.CustomPaletteHex.Count > 0)
            {
                // Custom hex renklerini kullan - palet seçimlerini yoksay
                Vm.PaletteColors = new List<string>(); // Boş bırak, custom_palette_hex kullanılacak
            }
            else
            {
                // Normal palet renklerini kullan
                Vm.PaletteColors = GetCheckedValues(cmbPalette);
                Vm.CustomPaletteHex = new List<string>();
            }
            
            // Arka plan modu - ComboBox'tan seçilen metni al
            var bgModeItem = cmbBackgroundMode.SelectedItem as System.Windows.Controls.ComboBoxItem;
            Vm.BackgroundMode = bgModeItem?.Content?.ToString() ?? "AUTO";
            
            Vm.BackgroundTolerance = double.Parse(txtBackgroundTolerance.Text, System.Globalization.CultureInfo.InvariantCulture);
            Vm.BackgroundColor = txtBackgroundColor.Text;
            Vm.Gap = double.Parse(txtGap.Text, System.Globalization.CultureInfo.InvariantCulture);
            Vm.LaserTolerance = double.Parse(txtLaser.Text, System.Globalization.CultureInfo.InvariantCulture);
            Vm.Density = sldDensity.Value;
            Vm.EdgeSensitivity = sldEdge.Value;
            Vm.DetailSensitivity = sldDetail.Value;
            Vm.BackgroundThreshold = (int)sldBg.Value;
            
            // Kalıp boyutu seçimi
            var templateItem = cmbTemplateSize.SelectedItem as System.Windows.Controls.ComboBoxItem;
            string templateTag = templateItem?.Tag?.ToString() ?? "500,700";
            
            if (templateTag == "custom")
            {
                // Özel ölçü kullan
                Vm.FabricWidthMm = double.Parse(txtWidth.Text, System.Globalization.CultureInfo.InvariantCulture);
                Vm.FabricHeightMm = string.IsNullOrWhiteSpace(txtHeight.Text)
                    ? (double?)null
                    : double.Parse(txtHeight.Text, System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                // Standart kalıp boyutu kullan
                var parts = templateTag.Split(',');
                Vm.FabricWidthMm = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                Vm.FabricHeightMm = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                // TextBox'ları da güncelle
                txtWidth.Text = parts[0];
                txtHeight.Text = parts[1];
            }
            
            // Mod seçimi - ComboBox'tan seçilen metni al ve ilk kelimeyi kullan (FULL, EDGE, vb.)
            var modeItem = cmbMode.SelectedItem as System.Windows.Controls.ComboBoxItem;
            string modeText = modeItem?.Content?.ToString() ?? "FULL";
            Vm.Mode = modeText.Split(' ')[0]; // İlk kelimeyi al (FULL, EDGE, FILL, SCATTER, BUDGET)
            
            Vm.BudgetTl = double.Parse(txtBudget.Text, System.Globalization.CultureInfo.InvariantCulture);
            Vm.StoneUnitPriceTl = string.IsNullOrWhiteSpace(txtStoneUnitPrice.Text)
                ? (double?)null
                : double.Parse(txtStoneUnitPrice.Text, System.Globalization.CultureInfo.InvariantCulture);
            Vm.Sprinkle = chkSprinkle.IsChecked.GetValueOrDefault();
            Vm.EdgeOnly = chkEdgeOnly.IsChecked.GetValueOrDefault(); // Artık checkbox ile kontrol ediliyor
            Vm.EdgeThreshold = 80;
        }

        private List<(string Name, string Hex, double Percentage)> GetCheckedAnalyzedColors()
        {
            var result = new List<(string Name, string Hex, double Percentage)>();
            
            foreach (var item in cmbAnalyzedColors.Items.OfType<ListBoxItem>())
            {
                var checkBox = item.Content as CheckBox;
                if (checkBox != null && checkBox.IsChecked.GetValueOrDefault())
                {
                    var content = checkBox.Content.ToString();
                    // Parse: "ColorName (#RRGGBB) - %XX.X"
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"(.+?)\s+\((#[0-9A-Fa-f]{6})\)\s+-\s+%([\d.]+)");
                    if (match.Success)
                    {
                        result.Add((match.Groups[1].Value, match.Groups[2].Value, double.Parse(match.Groups[3].Value)));
                    }
                }
            }
            
            return result;
        }

        // ExcludeColor_Click fonksiyonu kaldırıldı - artık arkaplan rengi analizi ile kullanılıyor

        private void InteractiveSelect_Click(object sender, RoutedEventArgs e)
        {
            _interactiveSelectionMode = true;
            _interactivePoints.Clear();
            _invertSelection = false;
            txtSelectionMode.Text = "🎯 Mod: Alan Seçimi Aktif - Önizlemede noktalara tıklayın";
            txtStatus.Text = "Önizleme üzerinde desenin içini doldurmak istediğiniz alanlara tıklayın. Bitirince 'ÖNİZLEME OLUŞTUR'a basın.";
        }

        // SelectEdges_Click fonksiyonu kaldırıldı - edge_only checkbox'ı doğrudan kullanılabilir

        private static List<string> GetCheckedValues(ListBox list)
        {
            return list.Items.OfType<ListBoxItem>()
                .Select(item => item.Content as CheckBox)
                .Where(checkBox => checkBox != null && checkBox.IsChecked.GetValueOrDefault())
                .Select(checkBox => checkBox.Content.ToString())
                .ToList();
        }
    }
}
