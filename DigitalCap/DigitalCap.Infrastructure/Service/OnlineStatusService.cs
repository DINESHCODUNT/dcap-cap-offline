using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace DigitalCap.Infrastructure.Service
{
    public static class IsOnline
    {
        public static bool Mode { get; set; } = true;
        public static bool OnlineOrOffline { get; set; } = true;
        public static bool IsElectron { get; set; } = true;
    }

    public class OnlineStatusService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public OnlineStatusService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        public async Task<bool> DetermineOnlineStatus()
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 1500);
                bool online = reply.Status == IPStatus.Success;

                IsOnline.OnlineOrOffline = online;
                IsOnline.Mode = online;
                return online;
            }
            catch
            {
                // Fallback check
                bool networkUp = NetworkInterface.GetIsNetworkAvailable();
                IsOnline.OnlineOrOffline = networkUp;
                IsOnline.Mode = networkUp;
                return networkUp;
            }
        }
    }

    public class OnlineStatusMiddleware
    {
        private readonly RequestDelegate _next;

        public OnlineStatusMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, OnlineStatusService onlineStatusService)
        {
            bool isOnline = await onlineStatusService.DetermineOnlineStatus();
            context.Items["IsOnline"] = isOnline;
            await _next(context);
        }
    }
}
