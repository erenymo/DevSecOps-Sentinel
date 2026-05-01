using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sentinel.Domain.Entities
{
    public class PackageLicense : BaseEntity
    {
        public string Purl { get; set; } = string.Empty;
        public Guid LicenseId { get; set; }
        public virtual License License { get; set; } = null!;
    }
}
