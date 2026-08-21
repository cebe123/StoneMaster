using System.Collections.Generic;

namespace StoneMaster.Corel.Models
{
    public sealed class EngineRequest
    {
        public string image_path { get; set; }
        public double? width_mm { get; set; }
        public double? height_mm { get; set; }
        public string stone_size { get; set; } = "SS10";
        public List<string> stone_sizes { get; set; }
        public List<string> palette { get; set; }
        public List<string> palette_colors { get; set; }
        public double gap_mm { get; set; } = 0.5;
        public double laser_tolerance_mm { get; set; } = 0.3;
        public double density { get; set; } = 0.65;
        public double edge_sensitivity { get; set; } = 0.5;
        public double detail_sensitivity { get; set; } = 0.5;
        public int background_threshold { get; set; } = 245;
        public string background_mode { get; set; } = "LIGHT";
        public double background_tolerance { get; set; } = 28;
        public string background_color { get; set; } = "#F5F5F5";
        public string color_algorithm { get; set; } = "Lab";
        public int? max_colors { get; set; }
        public string mode { get; set; } = "FULL";
        public bool calculate_cost { get; set; } = true;
        public bool budget_enabled { get; set; }
        public double? target_budget_tl { get; set; }
        public double? stone_unit_price_tl { get; set; }
        public List<double> exclusion_rect { get; set; }
        public List<string> custom_palette_hex { get; set; }
        public bool sprinkle { get; set; }
        public bool exclude_dark_stones { get; set; }
        public int dark_stone_threshold { get; set; } = 70;
        public List<List<string>> excluded_stone_groups { get; set; }
        public bool edge_only { get; set; }
        public int edge_threshold { get; set; } = 80;
        public int analysis_max_dimension { get; set; } = 1600;
    }

    public sealed class EngineResponse
    {
        public bool success { get; set; }
        public string error { get; set; }
        public int stone_count { get; set; }
        public double total_cost_tl { get; set; }
        public int used_colors { get; set; }
        public double width_mm { get; set; }
        public double height_mm { get; set; }
        public double average_density { get; set; }
        public List<StonePlacement> stones { get; set; } = new List<StonePlacement>();
    }
}