using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Threading;
using StoneMaster.Corel.Models;

namespace StoneMaster.Corel.Corel
{
    internal static class StoneRenderer
    {
        public static void RenderPreview(global::Corel.Interop.VGCore.Layer layer, IList<StonePlacement> stones,
            double widthMm, double heightMm, CoordinateMapper mapper)
        {
            if (layer == null || stones == null || widthMm <= 0 || heightMm <= 0)
                return;

            for (var index = 0; index < stones.Count; index++)
            {
                var stone = stones[index];
                if (stone == null || stone.DiameterMm <= 0)
                    continue;
                double x, y;
                mapper.Map(stone, widthMm, heightMm, out x, out y);

                var radius = mapper.MapDiameter(stone.DiameterMm, widthMm) / 2.0;
                if (radius <= 0)
                    continue;
                global::Corel.Interop.VGCore.Shape shape = layer.CreateEllipse2(x, y, radius, radius, 0, 360, false);
                ApplyRgbFill(shape, stone.HexColor);
                RemoveOutline(shape);
                PumpUi(index);
            }
        }

        public static void RenderLaser(global::Corel.Interop.VGCore.Layer layer, IList<StonePlacement> stones,
            double widthMm, double heightMm, CoordinateMapper mapper)
        {
            if (layer == null || stones == null || widthMm <= 0 || heightMm <= 0)
                return;

            for (var index = 0; index < stones.Count; index++)
            {
                var stone = stones[index];
                if (stone == null || stone.LaserDiameterMm <= 0)
                    continue;
                double x, y;
                mapper.Map(stone, widthMm, heightMm, out x, out y);

                var radius = mapper.MapDiameter(stone.LaserDiameterMm, widthMm) / 2.0;
                if (radius <= 0)
                    continue;
                global::Corel.Interop.VGCore.Shape shape = layer.CreateEllipse2(
                    x, y, radius, radius, 0, 360, false);

                try { shape.Fill.ApplyNoFill(); } catch { }
                try
                {
                    shape.Outline.Color.RGBAssign(255, 0, 0);
                    shape.Outline.Width = 0.001; // hairline approximation in document units
                }
                catch { }
                PumpUi(index);
            }
        }

        private static void PumpUi(int index)
        {
            if (index % 50 != 0)
                return;

            var dispatcher = Dispatcher.FromThread(System.Threading.Thread.CurrentThread);
            dispatcher?.Invoke(DispatcherPriority.Background, new DispatcherOperationCallback(_ => null), null);
        }

        private static void ApplyRgbFill(dynamic shape, string hex)
        {
            ParseHex(hex, out var r, out var g, out var b);
            try
            {
                shape.Fill.UniformColor.RGBAssign(r, g, b);
            }
            catch
            {
                try { shape.Fill.UniformColor.RGBAssign(r, g, b); } catch { }
            }
        }

        private static void RemoveOutline(dynamic shape)
        {
            try { shape.Outline.SetNoOutline(); } catch { }
            try { shape.Outline.Color = null; } catch { }
        }

        private static void ParseHex(string hex, out int r, out int g, out int b)
        {
            var s = (hex ?? "#000000").TrimStart('#');
            if (s.Length != 6) s = "000000";
            r = int.Parse(s.Substring(0, 2), NumberStyles.HexNumber);
            g = int.Parse(s.Substring(2, 2), NumberStyles.HexNumber);
            b = int.Parse(s.Substring(4, 2), NumberStyles.HexNumber);
        }
    }
}