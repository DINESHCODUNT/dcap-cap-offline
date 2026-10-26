using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.Permissions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace DigitalCap.Infrastructure.Service
{
    public class UserPermissionService : IUserPermissionService
    {
        private readonly IUserPermissionRepository _userPermissionRepository;
        private readonly ILogger<UserPermissionService> _logger;
        private readonly ISecurityService _securityService;
        public UserPermissionService(
            IUserPermissionRepository userPermissionRepository,
            ILogger<UserPermissionService> logger,
            ISecurityService securityService)
        {
            _userPermissionRepository = userPermissionRepository;
            _logger = logger;
            _securityService = securityService;
        }

        // GET ALL
        public async Task<ServiceResult<List<UserPermission>>> GetAllPermissionsAsync(bool isActive = false)
        {
            try
            {
                var permissions = await _userPermissionRepository.GetAllPermissionsAsync();
                return ServiceResult<List<UserPermission>>.Success(permissions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching permissions");
                return ServiceResult<List<UserPermission>>.Failure("Internal server error");
            }
        }

        // ADD
        public async Task<ServiceResult<UserPermission>> AddPermissionAsync(UserPermission permission)
        {
            try
            {
                if (permission == null)
                    return ServiceResult<UserPermission>.Failure("Permission object is required");

                if (string.IsNullOrWhiteSpace(permission.PermissionName))
                    return ServiceResult<UserPermission>.Failure("Permission name is required");

                bool isSuccess = await _userPermissionRepository.AddPermissionAsync(permission);

                if (!isSuccess)
                    return ServiceResult<UserPermission>.Failure("Failed to insert permission");

                return ServiceResult<UserPermission>.Success(permission);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding permission");
                return ServiceResult<UserPermission>.Failure("Internal server error");
            }
        }

        // UPDATE
        public async Task<ServiceResult<UserPermission>> UpdatePermissionAsync(UserPermission permission)
        {
            try
            {
                if (permission == null)
                    return ServiceResult<UserPermission>.Failure("Permission object is required");

                bool isUpdated = await _userPermissionRepository.UpdatePermissionAsync(permission);

                if (!isUpdated)
                    return ServiceResult<UserPermission>.Failure("Permission not found or update failed");

                return ServiceResult<UserPermission>.Success(permission);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating permission");
                return ServiceResult<UserPermission>.Failure("Internal server error");
            }
        }

        // DELETE
        public async Task<ServiceResult<bool>> DeletePermissionAsync(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                    return ServiceResult<bool>.Failure("Invalid permission id");

                bool isDeleted = await _userPermissionRepository.DeletePermissionAsync(id);

                if (!isDeleted)
                    return ServiceResult<bool>.Failure("Permission not found");

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting permission");
                return ServiceResult<bool>.Failure("Internal server error");
            }
        }

        public async Task<ServiceResult<bool>> DeleteRolePermissionAsync(Guid id)
        {
            try
            {
                bool isDeleted = await _userPermissionRepository.DeleteRolePermissionAsync(id);

                return ServiceResult<bool>.Success(!isDeleted);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Failure($"Internal server error: {ex.Message}");
            }
        }
        public async Task<ServiceResult<List<Role>>> GetUserRoles()
        {
            var response = await _userPermissionRepository.GetAllRoles();

            return ServiceResult<List<Role>>.Success(response);
        }
        public async Task<ServiceResult<List<UserRolePermissionViewModel>>> GetRolePermission(string roleId)
        {

            var response = await _userPermissionRepository.GetRolePermission(roleId);

            return ServiceResult<List<UserRolePermissionViewModel>>.Success(response);
        }

        public async Task<ServiceResult<bool>> UpdateRolePermission(UserRolePermission rolepermission)
        {
            var response = await _userPermissionRepository.UpdateRolePermissionAsync(rolepermission);

            return ServiceResult<bool>.Success(response);
        }

        public async Task<ServiceResult<bool>> AddRolePermission([FromBody] UserRolePermission rolepermission)
        {
            var response = await _userPermissionRepository.AddRolePermissionAsync(rolepermission);
            return ServiceResult<bool>.Success(response);
        }

        public async Task<ServiceResult<List<UserRolePermissionViewModel>>> UpdatePermission(string Id, string RoleId, string PermissionId, string Key, bool Value)
        {
            try
            {

                if (string.IsNullOrEmpty(Id) || Id == Guid.Empty.ToString())
                {
                    //Add Permission
                    UserRolePermission permission = new UserRolePermission();
                    permission.Id = Guid.NewGuid();
                    permission.RoleId = Guid.Parse(RoleId);
                    permission.PermissionId = Guid.Parse(PermissionId);
                    permission = AssignProperty(Key, Value, permission);
                    await _userPermissionRepository.AddRolePermissionAsync(permission);
                }
                else
                {
                    UserRolePermission permission = _userPermissionRepository.GetRolePermissionById(Id).Result;
                    permission = AssignProperty(Key, Value, permission);
                    await _userPermissionRepository.UpdateRolePermissionAsync(permission);

                }
                var data = await _userPermissionRepository.GetRolePermission(RoleId);

                return ServiceResult<List<UserRolePermissionViewModel>>.Success(data);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<UserRolePermissionViewModel>>.Failure($"Internal server error: {ex.Message}");
            }
        }


        public UserRolePermission AssignProperty(string Key, bool Value, UserRolePermission permission)
        {
            switch (Key)
            {
                case "read":
                    permission.IsReadable = Value;
                    break;
                case "edit":
                    permission.IsEditable = Value;
                    break;
                case "delete":
                    permission.IsDeleted = Value;
                    break;
            }
            return permission;
        }
        public async Task<ServiceResult<object>> GetUserMenuPermissions(string username)
        {
            try
            {
                var currentUserResult = await _securityService.GetCurrentUserAsync();

                var currentUser = currentUserResult?.Data;

                var permissions = currentUser?.Permissions?.Select(p => p.ToString()).ToList() ?? new List<string>();

                var roles = currentUser?.Roles?.ToList() ?? new List<string>();

                PermissionViewModel? manageTank = null;
                PermissionViewModel? manageTemplate = null;
                PermissionViewModel? manageDescription = null;
                PermissionViewModel? manageGrading = null;
                PermissionViewModel? manageExport = null;

                try
                {
                    manageTank = await _userPermissionRepository.GetRolePermissionByUserName(username, "Manage Tank");
                    manageTemplate = await _userPermissionRepository.GetRolePermissionByUserName(username, "Manage Template");
                    manageDescription = await _userPermissionRepository.GetRolePermissionByUserName(username, "Manage Description");
                    manageGrading = await _userPermissionRepository.GetRolePermissionByUserName(username, "Manage Grades");
                    manageExport = await _userPermissionRepository.GetRolePermissionByUserName(username, "Manage Export");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load granular role permissions");
                }

                // If offline or permissions are empty, ensure default desktop permissions are active
                if (permissions.Count == 0)
                {
                    permissions.Add("ViewAbsProjectLandingPage");
                    permissions.Add("ManageTanks");
                }
                if (roles.Count == 0)
                {
                    roles.Add("Surveyor");
                }
                manageTank ??= new PermissionViewModel { Read = true, Edit = true, Delete = true };

                var response = new
                {
                    permissions, 
                    roles,
                    pagePermissions = new
                    {
                        manageTank,
                        manageTemplate,
                        manageDescription,
                        manageGrading,
                        manageExport
                    }
                };

                return ServiceResult<object>.Success(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching permissions, returning safe fallback");
                return ServiceResult<object>.Success(new
                {
                    permissions = new List<string> { "ViewAbsProjectLandingPage", "ManageTanks" },
                    roles = new List<string> { "Surveyor" },
                    pagePermissions = new
                    {
                        manageTank = new PermissionViewModel { Read = true, Edit = true, Delete = true }
                    }
                });
            }
        }


    }


}
