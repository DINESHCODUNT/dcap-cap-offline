using DigitalCap.Core.Enumerations;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace DigitalCap.Core.Interfaces.Service
{
    public interface ISecurityService
    {
        //Task<ApplicationDto> GetUser(Guid userId);
        //Task<ApplicationUser?> GetUser(Guid userId);
        // Task<IEnumerable<ApplicationDto>> GetAllUsers();
        // Task<IEnumerable<ApplicationDto>> GetClientUsers();
        // Task<IEnumerable<Permission>> GetPermissionsForRole(string role);
        //Task<bool> UpdateRolesForUser(WebApi.Core.ApplicationUser user, Guid userId, params string[] roles);
        //  Task<bool> UpdatePermissionsForUser(Guid userId, params Permission[] permissions);

        Task<ApplicationDto?> GetUser(Guid userId);
        Task<bool> UpdatePermissionsForUser(Guid userId, params Permission[] permissions);

        Task<bool> ToggleUserIsEnabled(Guid id, bool isEnabled);
        Task<ServiceResult<ApplicationDto>> GetCurrentUserAsync();
        IEnumerable<string> Roles { get; }
        Task<bool> UpdateRolesForUser(ApplicationDto userId, params string[] roles);
    }
}
