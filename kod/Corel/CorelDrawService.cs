using System;
using System.Collections.Generic;
using System.IO;
using Corel.Interop.VGCore;

namespace StoneMaster.Corel.Corel
{
    public sealed class CorelDrawService
    {
        // The interop contracts are embedded at build time; the Corel PIA is
        // not copied beside the Docker or included in the installer.
        private Application _app;

        public void Attach(object corelApplication)
        {
            _app = corelApplication as Application;
            if (_app == null)
                throw new ArgumentException("CorelDRAW uygulama nesnesine bağlanılamadı.", nameof(corelApplication));
        }

        public bool IsReady => _app != null;

        public Application Application => _app;

        public dynamic ActiveDocument()
        {
            if (_app == null || _app.ActiveDocument == null)
                throw new InvalidOperationException("Lütfen önce bir CorelDRAW belgesi açın.");
            return _app.ActiveDocument;
        }

        public BitmapContext GetSelectedBitmapContext()
        {
            Document doc = _app.ActiveDocument;
            dynamic app = _app;
            dynamic selection = app.ActiveSelection;
            Shape shape = GetSelectedOrFirstBitmap(doc, selection);

            var temp = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StoneMaster", "temp",
                Guid.NewGuid().ToString("N") + ".jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(temp));

            // ExportBitmap with cdrSelection needs this shape to be the active
            // CorelDRAW selection. PreserveSelection prevents the export from
            // changing the user's selection state permanently.
            var preserveSelection = doc.PreserveSelection;
            doc.PreserveSelection = true;
            doc.ClearSelection();
            shape.AddToSelection();

            // cdrJPEG = 774, cdrSelection = 2, cdrRGBColorImage = 4
            try
            {
                ExportFilter filter = doc.ExportBitmap(
                    temp,
                    cdrFilter.cdrJPEG,
                    cdrExportRange.cdrSelection,
                    cdrImageType.cdrRGBColorImage,
                    0,
                    0,
                    150,
                    150,
                    cdrAntiAliasingType.cdrNormalAntiAliasing,
                    false,
                    false,
                    false,
                    false,
                    cdrCompressionType.cdrCompressionNone,
                    (StructPaletteOptions)null,
                    (Rect)null);
                filter.Finish();
            }
            finally
            {
                doc.PreserveSelection = preserveSelection;
            }

            return new BitmapContext
            {
                ImagePath = temp,
                Left = Convert.ToDouble(shape.LeftX),
                Bottom = Convert.ToDouble(shape.BottomY),
                Width = Convert.ToDouble(shape.SizeWidth),
                Height = Convert.ToDouble(shape.SizeHeight)
            };
        }

        public BitmapContext ImportBitmapContext(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException("Seçilen görsel bulunamadı.", imagePath);

            Document doc = _app.ActiveDocument;
            Page page = doc.ActivePage;
            Layer layer = page.ActiveLayer;
            var optionsType = Type.GetType("Corel.Interop.VGCore.StructImportOptionsClass, Corel.Interop.VGCore", true);
            var options = (StructImportOptions)Activator.CreateInstance(optionsType);
            layer.Import(imagePath, cdrFilter.cdrAutoSense, options);

            Shape imported = null;
            for (int i = 1; i <= layer.Shapes.Count; i++)
            {
                var candidate = (Shape)layer.Shapes[i];
                if (Convert.ToInt32(candidate.Type) == 5)
                    imported = candidate;
            }

            if (imported == null)
                throw new InvalidOperationException("Görsel CorelDRAW çalışma alanına aktarılamadı.");

            doc.ClearSelection();
            imported.AddToSelection();

            return GetSelectedBitmapContext();
        }

        public List<double> GetSelectedExclusionRect(BitmapContext bitmapContext)
        {
            if (bitmapContext == null || bitmapContext.Width <= 0 || bitmapContext.Height <= 0)
                throw new InvalidOperationException("Önce bitmap seçin veya görsel aktarın.");

            dynamic selection = _app.ActiveSelection;
            if (GetShapeCount(selection) != 1)
                throw new InvalidOperationException("CorelDRAW’da taşsız bırakılacak tek bir alan nesnesi seçin.");

            Shape area = (Shape)selection.Shapes[1];
            var top = bitmapContext.Bottom + bitmapContext.Height;
            var x = (Convert.ToDouble(area.LeftX) - bitmapContext.Left) / bitmapContext.Width;
            var y = (top - Convert.ToDouble(area.BottomY) - Convert.ToDouble(area.SizeHeight)) / bitmapContext.Height;
            var width = Convert.ToDouble(area.SizeWidth) / bitmapContext.Width;
            var height = Convert.ToDouble(area.SizeHeight) / bitmapContext.Height;

            return new List<double>
            {
                Math.Max(0, Math.Min(1, x)),
                Math.Max(0, Math.Min(1, y)),
                Math.Max(0, Math.Min(1 - x, width)),
                Math.Max(0, Math.Min(1 - y, height))
            };
        }

        private static Shape GetSelectedOrFirstBitmap(dynamic document, dynamic selection)
        {
            var selectedCount = GetShapeCount(selection);
            if (selectedCount == 1)
            {
                Shape selected = (Shape)selection.Shapes[1];
                if (Convert.ToInt32(selected.Type) == 5) // cdrBitmapShape
                    return selected;
            }
            else if (selectedCount > 1)
            {
                throw new InvalidOperationException("Lütfen tek bir bitmap seçin.");
            }

            // No bitmap was selected. Use the first bitmap already placed on
            // the active page, so imported/open-document artwork is usable.
            dynamic bitmaps = document.ActivePage.FindShapes("", 5, true); // cdrBitmapShape
            if (bitmaps == null || bitmaps.Count == 0)
                throw new InvalidOperationException("Aktif belgede bitmap bulunamadı. Bir bitmap seçin veya Görsel Seç ile dosyadan açın.");

            return (Shape)bitmaps.Shapes[1];
        }

        private static int GetShapeCount(dynamic selection)
        {
            if (selection == null)
                return 0;

            try { return Convert.ToInt32(selection.Shapes.Count); }
            catch { return 0; }
        }

        public void ApplyGeneration(Models.EngineResponse response, BitmapContext bitmapContext, bool renderPreview, bool renderLaser, bool clearPrevious)
        {
            if (response == null || response.stones == null)
                throw new InvalidOperationException("Önce geçerli bir önizleme oluşturun.");

            var doc = ActiveDocument();
            dynamic commandGroup = null;

            try
            {
                commandGroup = doc.BeginCommandGroup("StoneMaster Generate");

                var page = doc.ActivePage;
                var layers = page.Layers;
                var previewLayer = LayerService.GetOrCreateLayer(layers, page, "STONE_PREVIEW", clearPrevious);
                var laserLayer = LayerService.GetOrCreateLayer(layers, page, "LAZER_KALIP_KESIM", clearPrevious);

                var ctx = bitmapContext ?? new BitmapContext
                {
                    Left = 0,
                    Bottom = 0,
                    Width = response.width_mm,
                    Height = response.height_mm
                };

                if (ctx.Width <= 0 || ctx.Height <= 0 || response.width_mm <= 0 || response.height_mm <= 0)
                    throw new InvalidOperationException("Görselin genişlik/yükseklik bilgisi geçersiz. Görseli yeniden seçip önizlemeyi tekrar oluşturun.");

                var mapper = new CoordinateMapper(
                    ctx.Left,
                    ctx.Bottom,
                    ctx.Width,
                    ctx.Height);

                if (renderPreview)
                    StoneRenderer.RenderPreview(previewLayer, response.stones, response.width_mm, response.height_mm, mapper);

                if (renderLaser)
                    StoneRenderer.RenderLaser(laserLayer, response.stones, response.width_mm, response.height_mm, mapper);
            }
            finally
            {
                try { doc.EndCommandGroup(); } catch { }
            }
        }
    }

    public sealed class BitmapContext
    {
        public string ImagePath { get; set; }
        public double Left { get; set; }
        public double Bottom { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
