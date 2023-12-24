namespace LoadBalancer.Middleware
{
    public class RequestForwardingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly HttpClient _httpClient;
        private readonly ILogger<RequestForwardingMiddleware> _logger;

        public RequestForwardingMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory, ILogger<RequestForwardingMiddleware> logger)
        {
            _next = next;
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                var request = context.Request;
                var method = request.Method;
                var path = request.Path;

                _logger.LogInformation("Forwarding request: {Method} {Path}", method, path);

                // Create a new HttpRequestMessage
                var forwardRequest = new HttpRequestMessage(new HttpMethod(method), "http://localhost:4001" + path);
                foreach (var header in request.Headers)
                {
                    if (!forwardRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && forwardRequest.Content != null)
                    {
                        forwardRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }
                }

                // Send the request to the other server
                var response = await _httpClient.SendAsync(forwardRequest);

                _logger.LogInformation("Received response: {StatusCode}", response.StatusCode);

                // Copy the response headers
                foreach (var header in response.Headers)
                {
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                }

                // Copy the response status code
                context.Response.StatusCode = (int)response.StatusCode;

                // Copy the response body
                await response.Content.CopyToAsync(context.Response.Body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while forwarding the request.");
                // Handle the exception or rethrow it if necessary
            }
        }
    }
}