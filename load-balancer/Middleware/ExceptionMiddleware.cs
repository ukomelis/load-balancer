using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace LoadBalancer.Middleware
{
    public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<ExceptionMiddleware> _logger = logger;

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred: {Message}", ex.StackTrace);

                if (ex is ArgumentNullException)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                }

                var response = new { message = ex.Message };
                var payload = JsonSerializer.Serialize(response);
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(payload);
            }
        }
    }
}