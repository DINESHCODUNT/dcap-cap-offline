using DigitalCap.Core.Enumerations;
using DigitalCap.Core.Helpers.Constants;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.Permissions;
using DigitalCap.Core.Security;
using DigitalCap.Core.ViewModels;
using DigitalCap.Infrastructure.Service;
using DigitalCap.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DigitalCap.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskController : ApiBaseController
    {
        private readonly ITaskService _taskService;
        private readonly IProjectService _projectService;
        private readonly ISecurityService _securityService;
        private readonly IUserPermissionRepository _userPermissionRepository;

        public TaskController(
            ITaskService taskService,
            IProjectService projectService,
            ISecurityService securityService,
            IUserPermissionRepository userPermissionRepository)
        {
            _taskService = taskService;
            _projectService = projectService;
            _securityService = securityService;
            _userPermissionRepository = userPermissionRepository;
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> Index(int? id)
        {
            if (!id.HasValue)
                return BadRequest(new { message = "Project ID is required" });

            var result = await _projectService.GetSummary(id.Value);
            if (!result.IsSuccess)
                return BadRequest(new { message = result.ErrorMessage });

            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> CloseTechnicalTasks(int id)
        {
            try
            {
                var result = await _taskService.CloseTechnicalTasks(id);
                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error closing technical tasks", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetTask([FromQuery] string type, [FromQuery] string id)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type) || !int.TryParse(id, out var projectId))
            {
                return BadRequest(new { success = false, message = "Invalid parameters" });
            }

            try
            {
                var result = await _taskService.GetTasks(projectId, type);
                if (!result.IsSuccess)
                    return BadRequest(new { success = false, message = result.ErrorMessage });

                return Ok(new { data = result.Data, total = result.Data?.Count() ?? 0 });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving tasks", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetCap2Task([FromQuery] string type, [FromQuery] string id)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type) || !int.TryParse(id, out var projectId))
            {
                return BadRequest(new { success = false, message = "Invalid parameters" });
            }

            try
            {
                var result = await _taskService.GetCap2Tasks(projectId, type);
                if (!result.IsSuccess)
                    return BadRequest(new { success = false, message = result.ErrorMessage });

                return Ok(new { data = result.Data, total = result.Data?.Count() ?? 0 });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving cap2 tasks", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetStatuses()
        {
            try
            {
                var result = await _taskService.GetStatuses();
                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving statuses", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetDetailsPartial(int taskId)
        {
            try
            {
                var result = await _taskService.GetTask(taskId);
                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving task details", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetUpdatePartial(int taskId)
        {
            try
            {
                var result = await _taskService.GetTask(taskId);
                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving task for update", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetInvoiceDetailsPartial(int taskId)
        {
            try
            {
                var result = await _taskService.GetTask(taskId);
                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving invoice details", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetInvoiceUpdatePartial(int taskId)
        {
            try
            {
                var result = await _taskService.GetTask(taskId);
                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving invoice update", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetCloseTasksPartial(int projectId, [FromQuery] string category)
        {
            try
            {
                var result = await _taskService.GetIncompleteTasks(projectId, category);
                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                var taskNames = result.Data?.Select(x => x.Task).ToList() ?? new List<string>();
                return Ok(taskNames);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving close tasks", error = ex.Message });
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Update([FromBody] SingleTask model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);
                var currentUser = CurrentUsername.ToLower();
                var result = await _taskService.Update(model, currentUser);
                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                if (model.ProjectId.HasValue)
                {
                    var percentCompleteResult = await _taskService.UpdateProjectPercentComplete(model.ProjectId.Value);
                    if (percentCompleteResult.IsSuccess)
                    {
                        await _projectService.UpdateProjectPercentComplete(model.ProjectId.Value, currentUser);
                    }
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating task", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> ProjectGrading(int projectId)
        {
            try
            {
                var result = await _taskService.Grading(projectId);
                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                return Ok(result.Data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving project grading", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> Share(int id)
        {
            try
            {
                var taskResult = await _taskService.GetCap2Task(id);
                if (!taskResult.IsSuccess)
                    return BadRequest(new { message = taskResult.ErrorMessage });

                var task = taskResult.Data;
                var model = new ShareTaskViewModel
                {
                    TaskId = task.ID.Value,
                    TaskName = task.Task
                };

                return Ok(model);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving task for sharing", error = ex.Message });
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Share([FromBody] ShareTaskViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (string.IsNullOrWhiteSpace(model.Recipients))
                    return BadRequest(new { message = "Recipients are required" });

                var recipients = model.Recipients.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                var result = await _taskService.Share(model.TaskId, model.Message, recipients);

                if (!result.IsSuccess)
                    return BadRequest(new { message = result.ErrorMessage });

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error sharing task", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> TaskDetails(int taskId)
        {
            try
            {
                var currentUserResult = await _securityService.GetCurrentUserAsync();
                if (!currentUserResult.IsSuccess || currentUserResult.Data == null)
                    return Unauthorized(new { message = "User not authenticated" });

                var currentUser = currentUserResult.Data;

                var taskResult = await _taskService.GetCap2Task(taskId);
                if (!taskResult.IsSuccess)
                    return BadRequest(new { message = taskResult.ErrorMessage });

                var task = taskResult.Data;
                if (!task.ProjectId.HasValue)
                    return BadRequest(new { message = "Task does not have a project ID" });

                var taskDetails = await _userPermissionRepository.GetRolePermissionByUserName(
                    currentUser.UserName,
                    EnumExtensions.GetDescription(ManagePages.RevenueTracker),
                    task.ProjectId.Value);

                if (taskDetails == null || (!Convert.ToBoolean(taskDetails?.Read) && !Convert.ToBoolean(taskDetails?.Edit)))
                {
                    return Forbid("Access denied");
                }

                task.Update = Convert.ToBoolean(taskDetails?.Edit);
                task.View = Convert.ToBoolean(taskDetails?.Read);

                var projectNameResult = await _projectService.GetProjectName(task.ProjectId.Value);
                if (projectNameResult.IsSuccess)
                {
                    task.ProjectName = projectNameResult.Data;
                }

                return Ok(task);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving task details", error = ex.Message });
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> SaveTask([FromBody] SaveTaskRequest? request)
        {
            try
            {
                var currentUser = CurrentUsername.ToLower();
                if (request == null || !ModelState.IsValid)
                    return BadRequest(ModelState);

                var taskResult = await _taskService.GetCap2Task(request.Id);
                if (!taskResult.IsSuccess)
                    return BadRequest(new { message = taskResult.ErrorMessage });

                var task = taskResult.Data;
                task.StatusId = request.Status;

                if (request.StatusDate.HasValue)
                    task.StatusDate = request.StatusDate.Value;

                if (!string.IsNullOrWhiteSpace(request.AssignedTo) && task.AssignedTo != request.AssignedTo)
                {
                    task.AssignedDate = DateTime.Now.Date;
                    task.AssignedTo = request.AssignedTo;
                }

                if (request.DueDate.HasValue)
                    task.DueDate = request.DueDate.Value;

                if (request.Value != null)
                    task.Value = request.Value;

                if (request.SurveyDays != null)
                    task.SurveyDays = request.SurveyDays;

                var updateResult = await _taskService.Update(task, currentUser);
                if (!updateResult.IsSuccess)
                    return BadRequest(new { message = updateResult.ErrorMessage });

                // Refresh task and update project percent complete
                var refreshedTaskResult = await _taskService.GetCap2Task(request.Id);
                if (refreshedTaskResult.IsSuccess && refreshedTaskResult.Data?.ProjectId.HasValue == true)
                {
                    var percentCompleteResult = await _taskService.UpdateProjectPercentComplete(refreshedTaskResult.Data.ProjectId.Value);
                    if (percentCompleteResult.IsSuccess)
                    {
                        await _projectService.UpdateProjectPercentComplete(refreshedTaskResult.Data.ProjectId.Value, currentUser);
                    }
                }

                var finalTaskResult = await _taskService.GetCap2Task(request.Id);
                if (!finalTaskResult.IsSuccess)
                    return BadRequest(new { message = finalTaskResult.ErrorMessage });

                return Ok(finalTaskResult.Data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error saving task", error = ex.Message });
            }
        }
    }
}
