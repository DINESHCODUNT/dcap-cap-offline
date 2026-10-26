using DigitalCap.Core.Helpers.Constants;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace DigitalCap.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceController : ApiBaseController
    {
        private readonly ITaskService _taskService;
        private readonly IProjectService _projectService;

        public InvoiceController(ITaskService taskService, IProjectService projectService)
        {
            _taskService = taskService;
            _projectService = projectService;
        }

        /// <summary>
        /// Invoice index: invoicing-category tasks grouped by type + project name.
        /// </summary>
        [HttpGet("[action]")]
        public async Task<IActionResult> Index(int? id)
        {
            if (!id.HasValue)
                return BadRequest(new { message = "Project ID is required" });

            var cap2Result = await _taskService.GetCap2Tasks(id.Value, type: null);
            if (!cap2Result.IsSuccess)
                return BadRequest(new { message = cap2Result.ErrorMessage });

            var tasks = cap2Result.Data?
                .Where(x => x.Category == CategoryNames.Invoicing)
                ?? Enumerable.Empty<SingleTask>();

            var model = new TasksViewModel(id, tasks)
            {
                ProjectId = id
            };

            var nameResult = await _projectService.GetProjectName(id.Value);
            if (nameResult.IsSuccess)
                model.ProjectName = nameResult.Data;

            return Ok(model);
        }

        /// <summary>
        /// Grid data (legacy GetTasks by project + type), same behavior as MVC Invoice/GetTask without Kendo server paging.
        /// </summary>
        [HttpGet("[action]")]
        public async Task<IActionResult> GetTask([FromQuery] string type, [FromQuery] string id)
        {
            if (string.IsNullOrEmpty(id)
                || string.IsNullOrEmpty(type)
                || !int.TryParse(id, out var projectId))
            {
                return BadRequest(new { success = false, message = "Invalid parameters" });
            }

            var result = await _taskService.GetTasks(projectId, type);
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.ErrorMessage });

            return Ok(new { data = result.Data, total = result.Data?.Count() ?? 0 });
        }

        /// <summary>
        /// Closes invoice tasks for the project (calls CAP.Close_InvoiceTasks — confirm name in DB).
        /// </summary>
        [HttpPost("[action]")]
        public async Task<IActionResult> CloseInvoiceTasks(int id)
        {
            var result = await _taskService.CloseInvoiceTasks(id);
            if (!result.IsSuccess)
                return BadRequest(new { message = result.ErrorMessage });

            return Ok(new { success = true });
        }
    }
}
