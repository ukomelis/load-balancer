using System.Net;
using System.Text;

namespace LoadBalancer.Middleware
{
    public class RequestForwardingMiddleware
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RequestForwardingMiddleware> _logger;

        private readonly List<string> _serverPool =
        [
            "http://localhost:4000",
            "http://localhost:4001",
            "http://localhost:4002",
            // Add more servers as needed
        ];

        // Uncomment this to use Docker host networking
        // private readonly List<string> _serverPool =
        // [
        //     "http://host.docker.internal:4000",
        //     "http://host.docker.internal:4001",
        //     "http://host.docker.internal:4002",
        //     // Add more servers as needed
        // ];

        private int _lastServerUsed = 0;
        private Timer _healthCheckTimer;
        private List<string> _offlineServers = new List<string>();

        public RequestForwardingMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory, ILogger<RequestForwardingMiddleware> logger)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _healthCheckTimer = new Timer(CheckServerHealth, null, 0, 1000 * 60 * 5); // Check server health every 5 minutes
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                var request = context.Request;
                var method = request.Method;
                var path = request.Path;

                _logger.LogInformation("Forwarding request: {Method} {Path}", method, path);

                // Select a server using round-robin strategy
                var server = _serverPool[_lastServerUsed];
                _lastServerUsed = (_lastServerUsed + 1) % _serverPool.Count;

                // Create a new HttpRequestMessage
                using var forwardRequest = new HttpRequestMessage(new HttpMethod(method), server + path);
        

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

        private async void CheckServerHealth(object? state)
        {
            for (int i = _serverPool.Count - 1; i >= 0; i--)
            {
                var server = _serverPool[i];
                try
                {
                    var response = await _httpClient.GetAsync(server);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Removing server {Server} from pool due to failed health check", server);
                        _offlineServers.Add(server);
                        _serverPool.RemoveAt(i);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Removing server {Server} from pool due to failed health check", server);
                    _offlineServers.Add(server);
                    _serverPool.RemoveAt(i);
                }
            }

            for (int i = _offlineServers.Count - 1; i >= 0; i--)
            {
                var server = _offlineServers[i];
                try
                {
                    var response = await _httpClient.GetAsync(server);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Adding server {Server} back to pool after successful health check", server);
                        _serverPool.Add(server);
                        _offlineServers.RemoveAt(i);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed health check for offline server {Server}", server);
                }
            }
        }
    }
}