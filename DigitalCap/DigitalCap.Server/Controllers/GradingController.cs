using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.Grading;
using DigitalCap.Core.Models.ImageDescription;
using DigitalCap.Core.ViewModels;
using DigitalCap.Infrastructure.Service;
using DigitalCap.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DigitalCap.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GradingController : ApiBaseController
    {
        private readonly IGradingService _gradingService;
        private readonly ITankService _tankService;
        public GradingController(IGradingService gradingService, ITankService tankService)
        {
            _gradingService = gradingService;
            _tankService = tankService;
        }

        [HttpGet("[action]")]
        public async Task<ActionResult> GetVesselType()
        {
            try
            {
                var vesselTypes = await _tankService.GetShipType();
                return Ok(vesselTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving vessel types", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<ActionResult> GetTemplateName([FromQuery] string vesselType = null)
        {
            try
            {
                var templates = await _gradingService.GetTemplateName(vesselType);
                return Ok(templates);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving templates", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<ActionResult> GetSectionNameByTemplateNameAndVesselType([FromQuery] string templateName, [FromQuery] string vesselType)
        {
            try
            {
                var sections = await _gradingService.GetSectionNameByTemplateNameAndVesselType(templateName, vesselType);
                return Ok(sections);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving sections", error = ex.Message });
            }
        }

        [HttpGet("[action]")]
        public async Task<ActionResult> GetSectionName(int templateId, string vesselType)
        {
            try
            {
                var sections = await _gradingService.GetGradingSections(templateId, vesselType);
                return Ok(sections);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving sections", error = ex.Message });
            }
        }
        [HttpGet("[action]")]
        public async Task<IActionResult> GetSectionGrading()
        {
            List<SectionGrading> sectionsGrading = new List<SectionGrading>();
            return Ok(sectionsGrading);
        }


        [HttpGet("[action]")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _gradingService.GetAllGradingsAsync();

            //if (!result.Success)
            //    return BadRequest(result.Message);

            return Ok(result);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> CreateGrading([FromBody] CreateGradingRequest model)
        {
            var result = await _gradingService.CreateGradingAsync(model);

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            return Ok("Grading created successfully");
        }


        [HttpPut("[action]")]
        public async Task<IActionResult> UpdateGrading([FromBody] CreateGradingRequest model)
        {

            var result = await _gradingService.UpdateGradingAsync(model);

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            return Ok("Grading updated successfully");
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> DeleteGrading(int id, [FromQuery] int tankId = 0)
        {
            var result = await _gradingService.DeleteGradingAsync(id, tankId);

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            return Ok(result);
        }


        [HttpGet("[action]")]
        public async Task<IActionResult> FilterMenuCustomization_Read([FromQuery] DataSourceRequest request)
        {
            try
            {
                var serviceResult = await _gradingService.GetAllGradingsAsync();

                if (!serviceResult.IsSuccess)
                {
                    return BadRequest(serviceResult.Message);
                }

                var allData = serviceResult.Data;
                var pagedData = allData;

                // Apply pagination
                if (request != null)
                {
                    if (request.Skip.HasValue && request.Take.HasValue)
                    {
                        pagedData = allData.Skip(request.Skip.Value).Take(request.Take.Value).ToList();
                    }
                    else if (request.Page.HasValue && request.PageSize.HasValue)
                    {
                        var skip = (request.Page.Value - 1) * request.PageSize.Value;
                        pagedData = allData.Skip(skip).Take(request.PageSize.Value).ToList();
                    }
                }

                // Return array directly (matching frontend expectation)
                return Ok(pagedData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving gradings", error = ex.Message });
            }
        }
        //[HttpGet("[action]")]
        //public async Task<IActionResult> FilterMenuCustomization_Read(int pageNumber = 1, int pageSize = 50)
        //{
        //    var result = await _gradingService.GetAllGrading_Pagination(pageNumber, pageSize);
        //    return Ok(result);
        //}
    }
}
