using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LoadBalancer.Middleware
{
    public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger _logger = logger;

    public async Task Invoke(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var responseTime = sw.Elapsed;

            var request = context.Request;
            var response = context.Response;

            var logData = new
            {
                RequestMethod = request.Method,
                RequestPath = request.Path,
                RequestProtocol = request.Protocol,
                RequestIP = context.Connection.RemoteIpAddress?.ToString(),
                RequestHost = request.Host,
                ResponseStatusCode = response.StatusCode,
                ResponseTime = responseTime
            };

            _logger.LogInformation("Request: {@logData}", logData);

            sw.Reset();
        }
    }
        
        private static async Task<string> ReadResponseBody(HttpResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(response.Body).ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);
            return text;
        }
    }
}