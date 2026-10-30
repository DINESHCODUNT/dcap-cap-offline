using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models.Permissions;
using DigitalCap.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace DigitalCap.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserPermissionController : ApiBaseController
    {
        private readonly IUserPermissionService _userPermissionService;
        private readonly IUserPermissionRepository _userPermissionRepository;
        private readonly IProjectRepository _projectRepository;

        public UserPermissionController(
            IUserPermissionService userPermissionService,
            IUserPermissionRepository userPermissionRepository,
            IProjectRepository projectRepository)
        {
            _userPermissionService = userPermissionService;
            _userPermissionRepository = userPermissionRepository;
            _projectRepository = projectRepository;
        }

        #region Permission Management

        [HttpGet("[action]")]
        public async Task<IActionResult> GetAllPermissionsAsync([FromQuery] bool isActive = false)
        {
            var response = await _userPermissionService.GetAllPermissionsAsync(isActive);

            if (!response.IsSuccess)
                return StatusCode(500, response);

            return Ok(response.Data);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> AddPermission([FromBody] UserPermission permission)
        {
            var response = await _userPermissionService.AddPermissionAsync(permission);

            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response.Data);
        }

        [HttpDelete("[action]")]
        public async Task<IActionResult> DeletePermission(Guid id)
        {
            var response = await _userPermissionService.DeletePermissionAsync(id);

            if (!response.IsSuccess)
                return NotFound(response);

            return Ok(response.Data);
        }

        #endregion

        #region Role Permission Management

        [HttpPost("[action]")]
        public async Task<IActionResult> AddRolePermission([FromBody] UserRolePermission rolepermission)
        {
            var response = await _userPermissionService.AddRolePermission(rolepermission);

            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response.Data);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateRolePermission([FromBody] UserRolePermission rolepermission)
        {
            var response = await _userPermissionService.UpdateRolePermission(rolepermission);

            if (!response.IsSuccess)
                return NotFound(response);

            return Ok(response.Data);
        }

        [HttpDelete("[action]")]
        public async Task<IActionResult> DeleteRolePermission(Guid id)
        {
            var response = await _userPermissionService.DeleteRolePermissionAsync(id);

            if (!response.IsSuccess)
                return NotFound(response);

            return Ok(response.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetRolePermission([FromQuery] string roleId)
        {
            if (string.IsNullOrEmpty(roleId))
                return BadRequest("RoleId is required");

            var response = await _userPermissionService.GetRolePermission(roleId);

            if (!response.IsSuccess)
                return NotFound(response);

            return Ok(response.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetUserRoles()
        {
            var response = await _userPermissionService.GetUserRoles();

            if (!response.IsSuccess)
                return StatusCode(500, response);

            return Ok(response.Data);
        }
        [HttpPost("[action]")]
        public async Task<IActionResult> UpdatePermission(string Id, string RoleId, string PermissionId, string Key, bool Value)
        {
            var result =await _userPermissionService.UpdatePermission(Id, RoleId, PermissionId, Key, Value);

            return Ok(result);

        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetUserMenuPermissions()
        {
            var username = CurrentUsername;

            if (string.IsNullOrEmpty(username))
                return Unauthorized();

            try
            {
                var result = await _userPermissionService.GetUserMenuPermissions(username);

                if (!result.IsSuccess)
                    return BadRequest(new { message = result.Message });

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error fetching menu permissions",
                    error = ex.Message
                });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetManageTankPermission([FromQuery] int? projectId, [FromQuery] string? imo)
        {
            var currentUserName = CurrentUsername
                ?? User.FindFirstValue(ClaimTypes.Upn)
                ?? User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? User.Identity?.Name
                ?? string.Empty;

            if (!projectId.HasValue && !string.IsNullOrWhiteSpace(imo))
            {
                if (int.TryParse(imo.Trim(), out var imoNum))
                {
                    var projectList = await _projectRepository.GetProjectListByIMO(imoNum);
                    if (projectList != null && projectList.Count > 0 && int.TryParse(projectList[0].Value, out var pid))
                    {
                        projectId = pid;
                    }
                }
            }

            var permission = await _userPermissionRepository.GetRolePermissionByUserName(currentUserName, "Manage Tank", projectId);

            return Ok(new
            {
                read = permission?.Read ?? false,
                edit = permission?.Edit ?? false,
                delete = permission?.Delete ?? false
            });
        }
        #endregion
    }

}
