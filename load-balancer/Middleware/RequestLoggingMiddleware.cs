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
            var sw = new Stopwatch();
            sw.Start();

            var request = context.Request;
            var ip = context.Connection.RemoteIpAddress?.ToString();
            var method = request.Method;
            var path = request.Path;
            var protocol = request.Protocol;
            var host = request.Host;
            var userAgent = request?.Headers?["User-Agent"].ToString();
            var accept = request?.Headers?["Accept"].ToString();

            // Replace the original Response.Body stream with a MemoryStream
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            // Read the response body and log it
            var responseContent = await ReadResponseBody(context.Response);
            // _logger.LogInformation("Response content: {Content}", responseContent);

            sw.Stop();

            var statusCode = context.Response.StatusCode;
            _logger.LogInformation("Received request from\nIP: {ip}\n{method} {path} {protocol}\nStatusCode: {statusCode}\nHost: {host}\nUser-Agent: {userAgent}\nAccept: {accept} \nExecution time: {elapsedMilliseconds}ms",
                ip, method, path, protocol, statusCode, host, userAgent, accept, sw.Elapsed.TotalMilliseconds);

            // Write the response body back to the original stream
            await responseBody.CopyToAsync(originalBodyStream);
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