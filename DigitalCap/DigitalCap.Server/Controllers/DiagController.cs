using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DigitalCap.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagController : ControllerBase
    {
        private readonly IConfiguration _config;

        public DiagController(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Anonymous endpoint — shows what token (if any) was sent and decodes its claims.
        /// Use this to diagnose 401 issues without needing to be authenticated.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("token-info")]
        public IActionResult TokenInfo()
        {
            var authHeader = Request.Headers["Authorization"].ToString();

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return Ok(new { error = "No Bearer token in request. The frontend is not sending the Authorization header." });

            var rawToken = authHeader["Bearer ".Length..].Trim();

            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(rawToken))
                    return Ok(new { error = "Token present but is not a valid JWT format." });

                var jwt = handler.ReadJwtToken(rawToken);

                return Ok(new
                {
                    header = new
                    {
                        alg = jwt.Header.Alg,
                        kid = jwt.Header.Kid
                    },
                    claims = jwt.Claims.Select(c => new { c.Type, c.Value }),
                    audience = jwt.Audiences,
                    issuer = jwt.Issuer,
                    validFrom = jwt.ValidFrom,
                    validTo = jwt.ValidTo,
                    isExpired = jwt.ValidTo < DateTime.UtcNow,
                    backendExpects = new
                    {
                        audience = _config["AzureAd:Audience"],
                        authority = _config["AzureAd:Authority"],
                        validIssuers = new[]
                        {
                            $"https://sts.windows.net/{_config["AzureAd:Authority"]?.Split('/')[4]}/",
                            $"https://login.microsoftonline.com/{_config["AzureAd:Authority"]?.Split('/')[4]}/v2.0"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { error = $"Failed to decode token: {ex.Message}" });
            }
        }

        /// <summary>
        /// Requires authentication — use this to confirm auth works end-to-end.
        /// </summary>
        [Authorize]
        [HttpGet("auth-check")]
        public IActionResult AuthCheck()
        {
            // Resolve username the same way ApiBaseController.CurrentUsername does
            var resolvedUsername =
                User.FindFirstValue("preferred_username")
                ?? User.FindFirstValue("unique_name")
                ?? User.FindFirstValue("upn")
                ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Email)
                ?? User.FindFirstValue(System.Security.Claims.ClaimTypes.Name)
                ?? User.Identity?.Name;

            return Ok(new
            {
                authenticated = true,
                identityName = User.Identity?.Name,
                resolvedUsername,
                claims = User.Claims.Select(c => new { c.Type, c.Value })
            });
        }
    }
}
