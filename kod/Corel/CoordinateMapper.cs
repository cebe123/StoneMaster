using StoneMaster.Corel.Models;

namespace StoneMaster.Corel.Corel
{
    public sealed class CoordinateMapper
    {
        private readonly double _left;
        private readonly double _top;
        private readonly double _width;
        private readonly double _height;

        public CoordinateMapper(double left, double bottom, double width, double height)
        {
            _left = left;
            _top = bottom + height;
            _width = width;
            _height = height;
        }

        // Engine: top-left origin, Y down.
        // CorelDRAW: document origin, Y up.
        public void Map(StonePlacement stone, double sourceWidthMm, double sourceHeightMm, out double x, out double y)
        {
            var px = stone.XMm / sourceWidthMm;
            var py = stone.YMm / sourceHeightMm;

            x = _left + px * _width;
            y = _top - py * _height;
        }

        public double MapDiameter(double diameterMm, double sourceWidthMm)
        {
            return diameterMm / sourceWidthMm * _width;
        }
    }
}