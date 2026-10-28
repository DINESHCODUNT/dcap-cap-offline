using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
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
        // Azure AD is what online features (sign-in, token validation, sync) depend on,
        // so reachability of its HTTPS endpoint is the meaningful "online" signal.
        // ICMP is avoided because ships and corporate networks often block it.
        private const string ProbeHost = "login.microsoftonline.com";
        private const int ProbePort = 443;
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        private static readonly SemaphoreSlim ProbeLock = new SemaphoreSlim(1, 1);
        private static DateTime _lastChecked = DateTime.MinValue;

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public OnlineStatusService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        /// <summary>
        /// Returns the cached status, re-probing at most once per <see cref="CacheDuration"/>.
        /// Concurrent callers never wait on more than one probe.
        /// </summary>
        public async Task<bool> DetermineOnlineStatus()
        {
            if (DateTime.UtcNow - _lastChecked < CacheDuration)
                return IsOnline.OnlineOrOffline;

            if (!await ProbeLock.WaitAsync(0))
                return IsOnline.OnlineOrOffline;

            try
            {
                var online = await ProbeAsync();
                IsOnline.OnlineOrOffline = online;
                IsOnline.Mode = online;
                _lastChecked = DateTime.UtcNow;
                return online;
            }
            finally
            {
                ProbeLock.Release();
            }
        }

        private static async Task<bool> ProbeAsync()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return false;

            try
            {
                using var client = new TcpClient();
                using var cts = new CancellationTokenSource(ProbeTimeout);
                await client.ConnectAsync(ProbeHost, ProbePort, cts.Token);
                return client.Connected;
            }
            catch
            {
                return false;
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
