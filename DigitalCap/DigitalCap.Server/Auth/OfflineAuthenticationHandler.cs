using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DigitalCap.Core.Interfaces.Repository;
using ElectronNET.API;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DigitalCap.WebApi.Auth
{
    /// <summary>
    /// Authenticates desktop requests while there is no connection to Azure AD.
    /// The identity is the user recorded in LogInData the last time they signed in online,
    /// which is how digital-cap-offline identifies the surveyor when offline.
    /// </summary>
    public class OfflineAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Offline";

        private readonly ISyncRepository _syncRepository;

        public OfflineAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISyncRepository syncRepository)
            : base(options, logger, encoder)
        {
            _syncRepository = syncRepository;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // The desktop backend only ever serves its own Electron window.
            if (!HybridSupport.IsElectronActive || !IsLocalRequest(Context))
                return AuthenticateResult.NoResult();

            var user = await _syncRepository.GetUserInfo();
            if (user == null || string.IsNullOrWhiteSpace(user.EmailId))
                return AuthenticateResult.Fail("No offline identity. Sign in online once before working offline.");

            var displayName = string.Join(" ", new[] { user.FirstName, user.LastName }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            var claims = new List<Claim>
            {
                new Claim("preferred_username", user.EmailId),
                new Claim(ClaimTypes.Email, user.EmailId),
                new Claim(ClaimTypes.Name, string.IsNullOrEmpty(displayName) ? user.EmailId : displayName),
                new Claim(ClaimTypes.NameIdentifier, user.Id ?? user.EmailId),
                new Claim("auth_mode", SchemeName)
            };

            Context.Items["AuthProvider"] = SchemeName;
            Context.Items["AuthUser"] = user.EmailId;

            var identity = new ClaimsIdentity(claims, SchemeName, "preferred_username", ClaimTypes.Role);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
        }

        private static bool IsLocalRequest(HttpContext context)
        {
            var remote = context.Connection.RemoteIpAddress;
            return remote == null || IPAddress.IsLoopback(remote);
        }
    }
}
