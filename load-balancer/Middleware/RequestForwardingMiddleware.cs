using System.Net;
using System.Text;

namespace LoadBalancer.Middleware
{
    public class RequestForwardingMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory, ILogger<RequestForwardingMiddleware> logger)
    {
        private readonly RequestDelegate _next = next;
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
        private readonly ILogger<RequestForwardingMiddleware> _logger = logger;

        public async Task Invoke(HttpContext context)
        {
            try
            {
                var request = context.Request;
                var method = request.Method;
                var path = request.Path;

                _logger.LogInformation("Forwarding request: {Method} {Path}", method, path);

                // Create a new HttpRequestMessage
                using var forwardRequest = new HttpRequestMessage(new HttpMethod(method), "http://localhost:4001" + path);

                // Forward the request headers
                foreach (var header in request.Headers)
                {
                    if (!forwardRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && forwardRequest.Content != null)
                    {
                        forwardRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }
                }

                // Forward the request body
                if (request.HasFormContentType)
                {
                    forwardRequest.Content = new FormUrlEncodedContent(request.Form.ToDictionary(x => x.Key, x => x.Value.ToString()));
                }
                else
                {
                    var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
                    forwardRequest.Content = new StringContent(requestBody, Encoding.UTF8, request.ContentType ?? "application/octet-stream");
                }

                // Send the request to the other server
                using var response = await _httpClient.SendAsync(forwardRequest);

                _logger.LogInformation("Received response: {StatusCode}", response.StatusCode);

                // Copy the response headers
                foreach (var header in response.Headers.Concat(response.Content.Headers))
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }

                // Copy the response status code
                context.Response.StatusCode = (int)response.StatusCode;

                // Copy the response body only for non-304 responses
                if (response.StatusCode != HttpStatusCode.NotModified)
                {
                    await response.Content.CopyToAsync(context.Response.Body);
                }
                
                // no _next context as it is the last middleware
                //await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while forwarding the request.");
                // Handle the exception or rethrow it if necessary
                //throw;
            }
        }
    }
}