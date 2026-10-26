using DigitalCap.Core.Interfaces.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using DigitalCap.WebApi.Controllers;

namespace DigitalCAP.APIController
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportAPIController : ApiBaseController
    {
        private readonly IReportAPIService _reportAPIService;
        private readonly IProjectAPIService _projectAPIService;
        private readonly ITemplateAPIService _templateAPIService;

        public ReportAPIController(
            IReportAPIService reportAPIService,
            IProjectAPIService projectAPIService,
            ITemplateAPIService templateAPIService)
        {
            _reportAPIService = reportAPIService;
            _projectAPIService = projectAPIService;
            _templateAPIService = templateAPIService;
        }

        [HttpGet("[action]")]
        public async Task<ActionResult> GetProjectSummary(int projectId)
        {
            var result = await _projectAPIService.GetSummary(projectId);
            return Ok(result);
        }

        [HttpGet("[action]")]
        public async Task<ActionResult> ExportProjects()
        {
            var result = await _reportAPIService.ExportAllProjectsAsExcelSpreadsheet();
            return Ok(result);
        }

        [HttpGet("[action]")]
        public async Task<ActionResult> ExportAllProjectsGrid()
        {
            var result = await _reportAPIService.ExportAllProjectsGridAsExcelSpreadsheet();
            return Ok(result);
        }

        [HttpGet("[action]")]
        public async Task<ActionResult> ExportProjectsGridPartial(int minRange, int maxRange)
        {
            var result = await _reportAPIService.ExportProjectsPartial(minRange, maxRange);
            return Ok(result);
        }

        //[HttpGet("[action]")]
        //public async Task<ActionResult> DownloadBlankReport(int projectId)
        //{
        //    var result = await _templateAPIService.PopulateTemplate(projectId);
        //    return Ok(result);
        //}
    }
}
