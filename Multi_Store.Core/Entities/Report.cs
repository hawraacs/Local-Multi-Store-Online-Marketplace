using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    public class Report
    {
        public int ReportID { get; set; }

        // Who filed it
        public int? ReporterCustomerID { get; set; }
        public Customer? ReporterCustomer { get; set; }

        public int? ReporterStoreID { get; set; }   // stores can report customers too
        public Store? ReporterStore { get; set; }

        // What it's about
        public string TargetType { get; set; } = string.Empty; // "Product", "Store", "Customer"
        public int TargetId { get; set; }

        public string Reason { get; set; } = string.Empty;      // "Spam", "Hate speech", etc.
        public string? Description { get; set; }

        public string Status { get; set; } = "Pending Review";  // Pending Review / Resolved / Dismissed
        public string? AdminNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
    }
}
