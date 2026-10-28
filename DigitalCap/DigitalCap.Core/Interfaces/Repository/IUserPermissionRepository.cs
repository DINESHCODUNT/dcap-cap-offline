using DigitalCap.Core.Models.Permissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalCap.Core.Interfaces.Repository
{
    public interface IUserPermissionRepository
    {
        Task<PermissionViewModel> GetRolePermissionByUserName(string username, string viewname, int? projectId = null);
        Task<List<int>> GetCurrentUserProjects(string username);

        Task<List<UserPermission>> GetAllPermissionsAsync();
        Task<bool> AddPermissionAsync(UserPermission permission);
        Task<bool> UpdateRolePermissionAsync(UserRolePermission rolepermission);
        Task<bool> AddRolePermissionAsync(UserRolePermission rolepermission);
        Task<bool> DeleteRolePermissionAsync(Guid id);
        Task<bool> UpdatePermissionAsync(UserPermission permission);
        Task<bool> DeletePermissionAsync(Guid id);
        Task<UserRolePermission> GetRolePermissionById(string id);
        Task<List<Role>> GetAllRoles();
        Task<List<UserRolePermissionViewModel>> GetRolePermission(string id);

        /// <summary>
        /// Desktop offline only: the user's roles, permission claims and AspNetUsers id stored at download time.
        /// Returns null when the unit of work is not using the local database or nothing was stored for the user.
        /// </summary>
        Task<DigitalCap.Core.Models.DataTransfer.OfflineUserSecurity?> GetOfflineUserSecurityAsync(string userName);
    }
}
