using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sentinel.Application.DTOs.Responses
{
    public class BaseResponse<T>
    {
        public T? Data { get; init; } // C# 9+ init ile değişmezlik (immutability)
        public bool Success { get; init; }
        public string? Message { get; init; }
        public List<string>? Errors { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        public static BaseResponse<T> Ok(T data, string message = "Success")
            => new() { Data = data, Success = true, Message = message };

        public static BaseResponse<T> Fail(string error)
            => new() { Success = false, Errors = new List<string> { error } };
        public static BaseResponse<T> Fail(string error, string message = "Failed")
            => new() { Success = false, Errors = new List<string> { error }, Message = message };
        public static BaseResponse<T> Fail(List<string> errors)
            => new() { Success = false, Errors = errors };
        public static BaseResponse<T> Fail(List<string> errors, string message = "Failed")
            => new() { Success = false, Errors = errors, Message = message };
    }
}
