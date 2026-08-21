namespace StoneMaster.Corel.Models
{
    public sealed class StonePlacement
    {
        public double XMm { get; set; }
        public double x_mm { get { return XMm; } set { XMm = value; } }
        public double YMm { get; set; }
        public double y_mm { get { return YMm; } set { YMm = value; } }
        public double DiameterMm { get; set; }
        public double diameter_mm { get { return DiameterMm; } set { DiameterMm = value; } }
        public double LaserDiameterMm { get; set; }
        public double laser_diameter_mm { get { return LaserDiameterMm; } set { LaserDiameterMm = value; } }
        public string StoneName { get; set; }
        public string stone_name { get { return StoneName; } set { StoneName = value; } }
        public string ColorName { get; set; }
        public string color_name { get { return ColorName; } set { ColorName = value; } }
        public string HexColor { get; set; }
        public string hex_color { get { return HexColor; } set { HexColor = value; } }
        public double Importance { get; set; }
    }
}