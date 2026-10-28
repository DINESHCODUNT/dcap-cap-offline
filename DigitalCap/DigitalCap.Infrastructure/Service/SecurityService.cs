using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Security;
using DigitalCap.Core.Security.Extensions;
using DigitalCap.WebApi.Core;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;


namespace DigitalCap.Infrastructure.Service
{
    public class SecurityService : ISecurityService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<Microsoft.AspNetCore.Identity.IdentityRole> _roleManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserAccountRepository _userAccountRepository;
        private readonly IUserPermissionRepository _userPermissionRepository;

        public SecurityService(
        UserManager<ApplicationUser> userManager, RoleManager<Microsoft.AspNetCore.Identity.IdentityRole> roleManager
            , IHttpContextAccessor httpContextAccessor, IUserAccountRepository userAccountRepository
            , IUserPermissionRepository userPermissionRepository
            )
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _httpContextAccessor = httpContextAccessor;
            _userAccountRepository = userAccountRepository;
            _userPermissionRepository = userPermissionRepository;
        }

        public async Task<ServiceResult<ApplicationDto>> GetCurrentUserAsync_Temp()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return ServiceResult<ApplicationDto>.Failure("No HTTP context");

            var claimsPrincipal = httpContext.User;
            if (claimsPrincipal == null)
                return ServiceResult<ApplicationDto>.Failure("No user context");

            if (claimsPrincipal.Identity == null || !claimsPrincipal.Identity.IsAuthenticated)
            {
                return ServiceResult<ApplicationDto>.Failure("User is not authenticated. Please sign in first.");
            }

            ApplicationUser user = null;

            // For JWT tokens, we might need to get user by email/name claim
            if (claimsPrincipal.Identity.IsAuthenticated)
            {
                // Try GetUserAsync first (works for cookie auth)
                user = await _userManager.GetUserAsync(claimsPrincipal);

                if (user == null)
                {
                    var emailClaim = claimsPrincipal.FindFirst(System.Security.Claims.ClaimTypes.Email)
                                  ?? claimsPrincipal.FindFirst("email")
                                  ?? claimsPrincipal.FindFirst("preferred_username");

                    if (emailClaim != null && !string.IsNullOrEmpty(emailClaim.Value))
                    {
                        user = await _userManager.FindByEmailAsync(emailClaim.Value);
                    }
                }

                if (user == null)
                {
                    var nameIdClaim = claimsPrincipal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                                  ?? claimsPrincipal.FindFirst("sub");

                    if (nameIdClaim != null && Guid.TryParse(nameIdClaim.Value, out var userId))
                    {
                        user = await _userManager.FindByIdAsync(userId.ToString());
                    }
                }
            }

            if (user == null)
                return ServiceResult<ApplicationDto>.Failure("User not found in database");

            await MapUserDetails(user);

            var dto = new ApplicationDto
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                IsEnabled = user.IsEnabled,
                Roles = user.Roles ?? Array.Empty<string>(),
                Permissions = user.Permissions,
                UserAccount = user.UserAccount
            };

            return ServiceResult<ApplicationDto>.Success(dto);
        }
        public async Task<ServiceResult<ApplicationDto>> GetCurrentUserAsync()
        {
            try
            {
                var claimsPrincipal = _httpContextAccessor.HttpContext?.User;

                if (claimsPrincipal == null)
                    return ServiceResult<ApplicationDto>.Failure("No user context");

                // Azure AD v2 tokens use preferred_username; v1 tokens use unique_name.
                // claimsPrincipal.Identity.Name is null for v2 tokens without explicit NameClaimType mapping,
                // so we resolve the email/UPN from claims directly.
                var email = claimsPrincipal.FindFirstValue("preferred_username")
                    ?? claimsPrincipal.FindFirstValue("unique_name")
                    ?? claimsPrincipal.FindFirstValue("upn")
                    ?? claimsPrincipal.FindFirstValue(ClaimTypes.Email)
                    ?? claimsPrincipal.FindFirstValue(ClaimTypes.Name)
                    ?? claimsPrincipal.Identity?.Name;

                if (string.IsNullOrEmpty(email))
                    return ServiceResult<ApplicationDto>.Failure("No user identity in token");

                ApplicationUser? user = null;
                try
                {
                    // 1. Try exact email match
                    user = await _userManager.FindByEmailAsync(email);

                    // 2. Try by username (some systems store email as username)
                    if (user == null)
                        user = await _userManager.FindByNameAsync(email);

                    // 3. Try by the username prefix before '@' (handles domain mismatch between AD and DB)
                    if (user == null && email.Contains('@'))
                    {
                        var usernamePart = email.Split('@')[0];
                        user = await _userManager.FindByNameAsync(usernamePart);
                        if (user == null)
                            user = await _userManager.Users
                                .FirstOrDefaultAsync(u => u.UserName != null && u.UserName.StartsWith(usernamePart));
                    }
                }
                catch
                {
                    // Ignore offline or database query errors
                }

                if (user != null)
                {
                    try
                    {
                        await MapUserDetails(user);
                    }
                    catch
                    {
                        // Ignore mapping error on offline DB
                    }

                    var dto = new ApplicationDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        UserName = user.UserName,
                        IsEnabled = user.IsEnabled,

                        Roles = user.Roles ?? Array.Empty<string>(),
                        Permissions = user.Permissions,

                        UserAccount = user.UserAccount
                    };

                    return ServiceResult<ApplicationDto>.Success(dto);
                }

                // Desktop offline: AspNetUsers is empty locally, so use the roles and permission claims that were
                // stored for this user when a project was downloaded (null when online or nothing was stored).
                var offlineSecurity = await _userPermissionRepository.GetOfflineUserSecurityAsync(email);
                if (offlineSecurity != null)
                {
                    return ServiceResult<ApplicationDto>.Success(new ApplicationDto
                    {
                        Id = string.IsNullOrWhiteSpace(offlineSecurity.AspNetUserId)
                            ? claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier) ?? email
                            : offlineSecurity.AspNetUserId,
                        Email = email,
                        UserName = email,
                        IsEnabled = true,
                        Roles = offlineSecurity.Roles.ToArray(),
                        Permissions = new HashSet<Permission>(offlineSecurity.Claims.ToPermissions())
                    });
                }

                // Fallback using token claims for offline mode or unmapped accounts
                var roles = claimsPrincipal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
                if (roles.Length == 0)
                {
                    roles = new[] { "Surveyor" };
                }
                var fallbackDto = new ApplicationDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = email,
                    UserName = claimsPrincipal.FindFirstValue(ClaimTypes.Name) ?? email,
                    IsEnabled = true,
                    Roles = roles,
                    Permissions = new HashSet<Permission> { Permission.ViewAbsProjectLandingPage, Permission.ViewAllProjects }
                };

                return ServiceResult<ApplicationDto>.Success(fallbackDto);
            }
            catch (Exception ex)
            {
                return ServiceResult<ApplicationDto>.Failure($"Error resolving user: {ex.Message}");
            }
        }

        private static class ErrorCodes
        {
            public const string UserAlreadyInRole = nameof(UserAlreadyInRole);
        }
        private async Task MapUserDetails(ApplicationUser user)
        {
            await MapIsEnabled(user);
            await MapRolesAndClaimsToUser(user);
            await MapUserAccountToUser(user);
        }


        private async Task MapIsEnabled(ApplicationUser user)
        {
            if (user == null)
                return;

            var lockoutEndDate = await _userManager.GetLockoutEndDateAsync(user);
            user.IsEnabled = !lockoutEndDate.HasValue;
        }

        private async Task MapUserAccountToUser(ApplicationUser user)
        {
            if (user == null)
                return;

            user.UserAccount = await _userAccountRepository.GetByAspNetId(user.Id);
        }

        private async Task MapRolesAndClaimsToUser(ApplicationUser user)
        {
            if (user == null)
                return;

            user.Roles = (await _userManager.GetRolesAsync(user)).ToList();
            var userRoles = _roleManager.Roles.Where(x => user.Roles.Contains(x.Name)).ToList();
            var permissions = new List<Permission>();

            foreach (var currentRole in userRoles)
            {
                var currentClaims = await _roleManager.GetClaimsAsync(currentRole);
                var currentPermissions = currentClaims.ToPermissions();
                permissions.AddRange(currentPermissions);
            }

            var userClaims = await _userManager.GetClaimsAsync(user);
            var userPermissions = userClaims.ToPermissions();
            permissions.AddRange(userPermissions);
            var distinctPermissions = permissions.Distinct().ToList();

            user.Permissions = new HashSet<Permission>(distinctPermissions);
        }
        public IEnumerable<string> Roles
    => _roleManager.Roles.Select(r => r.Name).ToList();


        //public async Task<bool> UpdateRolesForUser(string userId, params string[] roles)
        //{
        //    var user = await _userManager.FindByIdAsync(userId.ToString());

        //    if (user == null)
        //        return false;

        //    return await UpdateRolesForIdentityUser(user, roles);
        //}

        //private async Task<bool> UpdateRolesForIdentityUser(ApplicationUser user, params string[] roles)
        //{
        //    var currentRoles = await _userManager.GetRolesAsync(user);

        //    await _userManager.RemoveFromRolesAsync(user, currentRoles);
        //    await _userManager.AddToRolesAsync(user, roles);

        //    return true;
        //}

        public async Task<ApplicationDto?> GetUser(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
                return null;

            await MapUserDetails(user);

            return new ApplicationDto
            {
                Id = user.Id, // ✅ NO conversion needed
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                IsEnabled = user.IsEnabled,
                Roles = user.Roles ?? Array.Empty<string>(),
                Permissions = user.Permissions,
                UserAccount = user.UserAccount
            };
        }


        public async Task<bool> UpdateRolesForUser(ApplicationDto userId, params string[] roles)
        {
            if (userId == null)
                return false;

            var user = await _userManager.FindByIdAsync(userId.Id.ToString());
            if (user == null)
                return false;

            // Get existing roles
            var existingRoles = await _userManager.GetRolesAsync(user);

            // Remove existing roles
            if (existingRoles.Any())
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, existingRoles);
                if (!removeResult.Succeeded)
                    return false;
            }

            // If no new roles, we are done
            if (!(roles?.Any() ?? false))
                return true;

            // Validate roles against RoleManager
            var allValidRoles = await _roleManager.Roles
                .Select(r => r.Name)
                .ToListAsync();

            var validRoles = roles
                .Where(r => allValidRoles.Contains(r))
                .Distinct()
                .ToArray();

            if (!validRoles.Any())
                return true;

            // Add new roles
            var addResult = await _userManager.AddToRolesAsync(user, validRoles);

            return addResult.Succeeded
                || addResult.Errors.All(e => e.Code == ErrorCodes.UserAlreadyInRole);
        }

        public async Task<bool> UpdatePermissionsForUser(Guid userId, params Permission[] permissions)
        {
            var identityUser = await _userManager.FindByIdAsync(userId.ToString());

            if (identityUser == null)
                return false;

            return await UpdatePermissionsInternal(identityUser, permissions);

        }

        private async Task<bool> UpdatePermissionsInternal(ApplicationUser user, params Permission[] permissions)
        {
            var existingClaims = await _userManager.GetClaimsAsync(user);

            if (existingClaims.Any())
            {
                var removeResult = await _userManager.RemoveClaimsAsync(user, existingClaims);
                if (!removeResult.Succeeded)
                    return false;
            }

            if (!(permissions?.Any() ?? false))
                return true;

            var newClaims = permissions.Select(p => p.ToClaim());
            var addResult = await _userManager.AddClaimsAsync(user, newClaims);

            return addResult.Succeeded;
        }

        public async Task<bool> ToggleUserIsEnabled(Guid id, bool isEnabled)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
                return false;

            var lockoutEndDate = isEnabled
                                    ? (DateTimeOffset?)null
                                    : DateTimeOffset.MaxValue;
            var result = await _userManager.SetLockoutEndDateAsync(user, lockoutEndDate);
            return result.Succeeded;
        }
    }
}
