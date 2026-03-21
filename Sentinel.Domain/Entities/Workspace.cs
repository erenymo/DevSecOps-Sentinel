using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sentinel.Domain.Entities
{
    public class Workspace : BaseEntity
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid OwnerId { get; set; }

        public virtual ICollection<Module> Modules { get; set; } = new List<Module>();
    }
}
