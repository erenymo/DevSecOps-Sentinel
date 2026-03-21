using Sentinel.Domain.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sentinel.Application.Abstractions
{
    public interface IScannerService
    {
        Task<Result<Guid>> RunScanAsync(Guid moduleId, string fileName, string fileContent);
    }
}
