using System.Collections.Generic;
using System.Windows.Media;
using StoneMaster.Corel.Models;

namespace StoneMaster.Corel.Services
{
    public sealed class PreviewService
    {
        public IEnumerable<PreviewDot> CreateDots(EngineResponse response, double previewWidth)
        {
            var scale = previewWidth / response.width_mm;
            var list = new List<PreviewDot>();

            foreach (var stone in response.stones)
            {
                list.Add(new PreviewDot
                {
                    X = stone.XMm * scale,
                    Y = stone.YMm * scale,
                    Diameter = System.Math.Max(2, stone.DiameterMm * scale),
                    Fill = (SolidColorBrush)new BrushConverter().ConvertFromString(stone.HexColor)
                });
            }

            return list;
        }
    }

    public sealed class PreviewDot
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Diameter { get; set; }
        public SolidColorBrush Fill { get; set; }
    }
}