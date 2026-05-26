
using System;

namespace Multi_Store.Services.Dtos
{
    public class SystemConfigDTO
    {
        public int ConfigID { get; set; }
        public string ConfigKey { get; set; } = string.Empty;
        public string ConfigValue { get; set; } = string.Empty;
        public string ValueType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? UpdatedBy { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual User? Updater { get; set; }
    }
}