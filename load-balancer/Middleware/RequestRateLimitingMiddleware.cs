using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace LoadBalancer.Middleware
{
    public class RequestRateLimitingMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        private readonly RequestDelegate _next = next;
        private readonly IMemoryCache _cache = cache;
        private readonly TimeSpan _expirationTime = TimeSpan.FromMinutes(1); // Set your expiration time
        private readonly int _requestLimit = 100; // Set your request limit


        public async Task Invoke(HttpContext context)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();

            if (ipAddress != null && _cache.TryGetValue(ipAddress, out int requestCount))
            {
                if (requestCount >= _requestLimit)
                {
                    context.Response.StatusCode = 429; // Too Many Requests
                    await context.Response.WriteAsync("Rate limit exceeded. Try again later.");
                    return;
                }
                else
                {
                    _cache.Set(ipAddress, requestCount + 1, _expirationTime);
                }
            }
            else if (ipAddress != null)
            {
                _cache.Set(ipAddress, 1, _expirationTime);
            }

            await _next(context);
        }
    }
}