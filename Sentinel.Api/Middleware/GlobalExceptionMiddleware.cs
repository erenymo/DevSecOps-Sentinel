using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentinel.Application.DTOs.Responses;

namespace Sentinel.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during the request.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        // Hata türüne göre Status Code belirleme
        var statusCode = exception switch
        {
            // İleride buraya Custom ValidationException ekleyebilirsin
            ArgumentException or InvalidOperationException => (int)HttpStatusCode.BadRequest,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            _ => (int)HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = statusCode;

        // Mesaj belirleme: S-SDLC gereği Prod ortamında detay gizliyoruz
        string message = _env.IsDevelopment()
            ? exception.Message
            : "An unexpected error occurred. Please contact technical support.";

        // Hata listesi (Environment Awareness)
        List<string>? errors = null;
        if (_env.IsDevelopment())
        {
            errors = new List<string> { exception.StackTrace ?? "No stack trace available." };
        }

        var response = new BaseResponse<object>
        {
            Success = false,
            Message = message,
            Errors = errors,
            Timestamp = DateTime.UtcNow
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var result = JsonSerializer.Serialize(response, jsonOptions);

        await context.Response.WriteAsync(result);
    }
}