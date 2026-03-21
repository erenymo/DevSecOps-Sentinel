using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sentinel.Domain.Entities
{
    public class License : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Type { get; set; } // e.g., Copyleft, Permissive [cite: 21]
        public string RiskLevel { get; set; } = "Low";

        public virtual ICollection<Component> Components { get; set; } = new List<Component>();
    }
}
