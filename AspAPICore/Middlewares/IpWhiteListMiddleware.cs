using Microsoft.AspNetCore.Http;
using NLog;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace AspAPICore.Middlewares
{
    public class IpWhiteListMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        private readonly List<string> _ipWhiteList;
        public IpWhiteListMiddleware(RequestDelegate next, ILogger logger)
        {
            _next = next;
            _logger = logger;
            // TODO: Read ip white list from config
            _ipWhiteList = new List<string>() { "127.0.0.1", "::1" }; // Add allow ip here, TODO: Remove "::1" to block request from local
        }

        public async Task Invoke(HttpContext context)
        {
            // Only verify IP for the request which not GET method
            if (context.Request.Method != "GET")
            {
                var remoteIP = context.Connection.RemoteIpAddress;
                _logger.Error($"Request from Remote IP address: {remoteIP}");
                // Return 401 Forbidden if not valid IP
                if (IsDangerousIP(remoteIP))
                {
                    _logger.Error($"Forbidden Request from Remote IP address: {remoteIP}");

                    context.Response.StatusCode = 401;
                    return;
                }
                // Else, Continue down the Middleware pipeline
                await _next.Invoke(context);
            }
        }

        private bool IsDangerousIP(IPAddress ipAddress)
        {
            return !_ipWhiteList.Contains(ipAddress.ToString());
        }
    }
}
