using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sentinel.Domain.Responses
{
    public class Result<T>
    {
        public T? Data { get; set; }
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }
        public DateTime ResponseTime { get; set; } = DateTime.UtcNow;

        public static Result<T> Success(T data, string message = "İşlem başarılı.")
            => new() { Data = data, IsSuccess = true, Message = message };

        public static Result<T> Failure(string error)
            => new() { IsSuccess = false, Errors = new List<string> { error } };

        public static Result<T> Failure(List<string> errors)
            => new() { IsSuccess = false, Errors = errors };
    }
}
