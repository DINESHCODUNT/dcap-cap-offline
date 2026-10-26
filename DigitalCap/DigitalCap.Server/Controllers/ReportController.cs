using DigitalCap.Core.Interfaces.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;

namespace DigitalCap.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : ApiBaseController
    {
        private readonly IProjectService _projectService;
        private readonly IReportService _reportService;

        public ReportController(IProjectService projectService, IReportService reportService){
            _projectService = projectService;
            _reportService = reportService;
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> Index([FromQuery] int projectId)
        {
            try
            {
                var result = await _projectService.GetSummary(projectId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving project summary", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> ExportProjects()
        {
            try
            {
                var exportedProjects = await _reportService.ExportAllProjectsAsExcelSpreadsheet();
                var result = new FileContentResult(exportedProjects, "application/vnd.ms-excel")
                {
                    FileDownloadName = $"Digital CAP - All Projects - {DateTime.Now.ToString("MM_dd_yyyy_HH_mm_ss")}.xls"
                };

                return result;
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error exporting projects", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> ExportAllProjectsGrid()
        {
            try
            {
                var exportedProjects = await _reportService.ExportAllProjectsGridAsExcelSpreadsheet();

                var result = new FileContentResult(exportedProjects, "application/vnd.ms-excel")
                {
                    FileDownloadName = $"Digital CAP - All Projects - {DateTime.Now.ToString("MM_dd_yyyy_HH_mm_ss")}.xls"
                };

                return result;
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error exporting projects grid", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> ExportProjectsGridPartial([FromQuery] int? minRange, [FromQuery] int? maxRange)
        {
            if (!minRange.HasValue || !maxRange.HasValue)
            {
                return BadRequest("Invalid minRange or maxRange");
            }

            try
            {
                var result = await _reportService.ExportProjectsPartial(minRange.Value, maxRange.Value);
                return Ok(new { result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.ToString() });
            }
        }

        // [HttpGet("[action]")]
        //public async Task<IActionResult> DownloadBlankReport([FromQuery] int projectId)
        //{
        //    try
        //    {
        //        var populatedTemplate = await _templateService.PopulateTemplate(projectId);
        //        var result = new FileContentResult(populatedTemplate, "application/zip")
        //        {
        //            FileDownloadName = "CAP Report.zip"
        //        };

        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { message = "Error downloading blank report", error = ex.Message });
        //    }
        //}

    }
}
