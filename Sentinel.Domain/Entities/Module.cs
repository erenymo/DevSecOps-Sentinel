using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sentinel.Domain.Entities
{
    public class Module : BaseEntity
    {
        [Required]
        public Guid WorkspaceId { get; set; }
        public virtual Workspace Workspace { get; set; } = null!;

        [Required, MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Ecosystem { get; set; } = "NuGet";

        [Required]
        public string RootPath { get; set; } = string.Empty;

        public virtual ICollection<Scan> Scans { get; set; } = new List<Scan>();
    }
}
