using DigitalCap.Core.Enumerations;
using DigitalCap.Core.Helpers.Constants;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.Permissions;
using DigitalCap.Core.Security;
using DigitalCap.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DigitalCap.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ApiBaseController
    {
        private readonly IProjectService _projectService;
        private readonly IFileStorageService _fileStorageService;
        private readonly ISecurityService _securityService;
        private readonly IUserPermissionRepository _userPermissionRepository;

        public FileController(
            IProjectService projectService,
            IFileStorageService fileStorageService,
            ISecurityService securityService,
            IUserPermissionRepository userPermissionRepository)
        {
            _projectService = projectService ;
            _fileStorageService = fileStorageService ;
            _securityService = securityService ;
            _userPermissionRepository = userPermissionRepository;
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> Index([FromQuery] int projectId, [FromQuery] bool clientPage = false, [FromQuery] int? fileId = null)
        {
            try
            {
                var currentUserResult = await _securityService.GetCurrentUserAsync();
                if (!currentUserResult.IsSuccess || currentUserResult.Data == null)
                    return Unauthorized(new { message = "User not authenticated" });

                var currentUser = currentUserResult.Data;
                var projectFiles = await _userPermissionRepository.GetRolePermissionByUserName(
                    currentUser.UserName,
                    EnumExtensions.GetDescription(ManagePages.ProjectFiles),
                    projectId);

                if (projectFiles == null || (!Convert.ToBoolean(projectFiles?.Edit) && !Convert.ToBoolean(projectFiles?.Read) && !Convert.ToBoolean(projectFiles?.Delete)))
                {
                    return Forbid("Access denied");
                }

                // Check if user is client role
                if (!clientPage)
                {
                    foreach (var role in FormValues.ClientRoles)
                    {
                        if (currentUser.Roles.Contains(role))
                        {
                            clientPage = true;
                            break;
                        }
                    }
                }

                var result = await _projectService.GetSummary(projectId);
                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                return Ok(new
                {
                    ProjectSummary = result.Data,
                    ClientPage = clientPage,
                    FileDownloadId = fileId?.ToString()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving file index", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public IActionResult GetDataTypes()
        {
            try
            {
                var result = FormValues.AdditionalFilesDataTypes.Select(x => new { Value = x.Value, Text = x.Text });
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving data types", error = ex.Message });
            }
        }

        [HttpPost("[action]")]
        [Consumes("multipart/form-data")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadProjectFile([FromForm] IFormFile uploadedFile, [FromForm] FileDataType dataType, [FromForm] int projectId)
        {
            try
            {
                if (uploadedFile == null)
                    return BadRequest(new { message = "File is required" });

                //var currentUser = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                var currentUser = CurrentUsername.ToLower();


                if (string.IsNullOrEmpty(currentUser))
                    return Unauthorized(new { message = "User not authenticated" });

                var storedFile = await _fileStorageService.SaveProjectFile(uploadedFile, dataType, projectId, currentUser);

                return Ok(new { Id = storedFile.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error uploading project file", error = ex.Message });
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> UploadTaskFile([FromForm] IFormFile uploadedFile, [FromForm] FileDataType dataType, [FromForm] int projectId, [FromForm] int taskId)
        {
            try
            {
                if (uploadedFile == null)
                    return BadRequest(new { message = "File is required" });

                // var currentUser = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var currentUser = CurrentUsername.ToLower();

                if (string.IsNullOrEmpty(currentUser))
                    return Unauthorized(new { message = "User not authenticated" });

                var storedFile = await _fileStorageService.SaveTaskFile(uploadedFile, dataType, projectId, taskId, currentUser);

                return Ok(new { Id = storedFile.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error uploading task file", error = ex.Message });
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Delete([FromQuery] int? fileId)
        {
            try
            {
                if (!fileId.HasValue)
                    return BadRequest(new { message = "File ID is required" });

                var currentUser = CurrentUsername.ToLower();
                if (string.IsNullOrEmpty(currentUser))
                    return Unauthorized(new { message = "User not authenticated" });

                await _fileStorageService.DeleteFile(fileId.Value, currentUser);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error deleting file", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetProjectFilesDataSource([FromQuery] int projectId, [FromQuery] FileDataType[] dataTypes, [FromQuery] bool isClientPage = false)
        {
            try
            {
                var files = await _fileStorageService.GetProjectFiles(projectId);

                // Filter by data types if provided
                if (dataTypes != null && dataTypes.Length > 0)
                {
                    files = files.Where(x => dataTypes.Contains(x.DataType));
                }

                // Filter by client availability if client page
                if (isClientPage)
                {
                    files = files.Where(x => x.AvailableToClient == true);
                }

                return Ok(new { data = files, total = files.Count() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving project files", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetProjectFilesView([FromQuery] int projectId)
        {
            try
            {
                var result = (await _fileStorageService.GetProjectFiles(projectId))
                    .OrderByDescending(f => f.LastModifiedDate)
                    .Take(3);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving project files view", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetProjectFiles([FromQuery] int projectId)
        {
            try
            {
                var result = await _fileStorageService.GetProjectFiles(projectId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving project files", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetTaskFilesDataSource([FromQuery] int taskId)
        {
            try
            {
                var files = await _fileStorageService.GetTaskFiles(taskId);
                return Ok(new { data = files, total = files.Count() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving task files", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetTaskFiles([FromQuery] int taskId)
        {
            try
            {
                var result = await _fileStorageService.GetTaskFiles(taskId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving task files", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetDownloadUrl([FromQuery] int fileId)
        {
            try
            {
                var result = await _fileStorageService.GetFileDownloadUrl(fileId);
                if (string.IsNullOrEmpty(result))
                    return NotFound(new { message = "File not found" });

                return Ok(new { url = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving download URL", error = ex.Message });
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> MoveFile([FromBody] MoveProjectFileViewModel fileViewModel)
        {
            try
            {
                if (fileViewModel == null)
                    return BadRequest(new { message = "File view model is required" });

                var currentUser = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUser))
                    return Unauthorized(new { message = "User not authenticated" });

                await _fileStorageService.MoveFile(fileViewModel.FileId, fileViewModel.FileDataType, currentUser);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error moving file", error = ex.Message });
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateFileMetadata([FromQuery] string id, [FromQuery] string name, [FromQuery] string dataType, [FromQuery] string availableToClient)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id) || !int.TryParse(id, out var fileId))
                    return BadRequest(new { message = "Invalid file ID" });

                if (string.IsNullOrWhiteSpace(dataType) || !int.TryParse(dataType, out var dataTypeInt))
                    return BadRequest(new { message = "Invalid data type" });

                if (string.IsNullOrWhiteSpace(availableToClient) || !bool.TryParse(availableToClient, out var availableToClientBool))
                    return BadRequest(new { message = "Invalid available to client value" });

                //var currentUser = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var currentUser = CurrentUsername.ToLower(); ;
                if (string.IsNullOrEmpty(currentUser))
                    return Unauthorized(new { message = "User not authenticated" });

                await _fileStorageService.UpdateFileMetadata(fileId, (FileDataType)dataTypeInt, name, availableToClientBool, currentUser);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating file metadata", error = ex.Message });
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ShareFile([FromQuery] int projectId, [FromQuery] string projectName, [FromQuery] string emailTo, [FromQuery] string fileId, [FromQuery] string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(emailTo))
                    return BadRequest(new { message = "Email recipient is required" });

                var scheme = Request.Scheme;
                var host = Request.Host.Value;
                var fileUrl = $"{scheme}://{host}/api/File/Index?projectId={projectId}&clientPage=true&fileId={fileId}";

                await _fileStorageService.ShareFile(projectId, projectName, emailTo, fileUrl, fileName);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error sharing file", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        [PermissionAuthorize(Permission.ClientDashboardDownloadDeliverables)]
        public async Task<IActionResult> DownloadClientDeliverables([FromQuery] int projectId)
        {
            try
            {
                var stream = await _fileStorageService.DownloadZip(projectId, FileDataType.ClientDeliverables, true);

                if (stream != null)
                {
                    return File(stream, "application/zip", $"Project-{projectId}-ClientDeliverables.zip");
                }

                return StatusCode(403, new { message = "Seems like you do not have rights to access to this resource." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error downloading client deliverables", error = ex.Message });
            }
        }
    }
}
