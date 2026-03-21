using Sentinel.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sentinel.Application.Abstractions
{
    public interface IParserStrategy
    {
        string Ecosystem {  get; }
        Task<List<Component>> ParseAsync(string fileContent, Guid scanId);
    }
}
