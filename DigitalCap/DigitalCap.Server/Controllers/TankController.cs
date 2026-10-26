using DigitalCap.Core.DTO;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.WebApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace DigitalCap.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TankController : ApiBaseController
    {
        private readonly ITankService _tankService;

        public TankController(ITankService tankService)
        {
            _tankService = tankService;
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateTankImageDescriptionDropdownCard([FromForm] int templateId, [FromForm] int projectId, [FromForm] Guid sectionId, [FromForm] int cardNumber, [FromForm] int value, [FromForm] string cardName)
        {
            var result = await _tankService.UpdateTankImageDescriptionDropdownCard(templateId, projectId, sectionId, cardNumber, value, cardName);

            return Ok(result.Data);
        }


        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateTankCardAdditionalDescription([FromForm] int templateId, [FromForm] int projectId, [FromForm] Guid sectionId, [FromForm] int cardNumber, [FromForm] string value, [FromForm] string cardName)
        {
            var result = await _tankService.UpdateTankCardAdditionalDescription(templateId, projectId, sectionId, cardNumber, value, cardName);

            return Ok(result.Data);
        }


        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateTankCardCurrentCondition([FromForm] int templateId, [FromForm] int projectId, [FromForm] Guid sectionId, [FromForm] int cardNumber, [FromForm] int value, [FromForm] string cardName)
        {
            var result = await _tankService.UpdateTankCardCurrentCondition(templateId, projectId, sectionId, cardNumber, value, cardName);

            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> UploadTankCardImage(IFormFile tankfile, [FromForm] int templateId, [FromForm] int projectId, [FromForm] Guid sectionId, [FromForm] int cardNumber, [FromForm] int imageId, [FromForm] bool replaceImage = false, [FromForm] string cardName = null)
        {
            var result = await _tankService.UploadTankCardImage(tankfile, templateId, projectId, sectionId, cardNumber, imageId, replaceImage, cardName);

            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> DeleteTankCardImage([FromForm] int templateId, [FromForm] int projectId, [FromForm] Guid sectionId, [FromForm] int cardNumber, [FromForm] int imageId)
        {
            var result = await _tankService.DeleteTankCardImage(templateId, projectId, sectionId, cardNumber, imageId);
            return Ok(result.Data);
        }


        [HttpGet("[action]")]
        public async Task<IActionResult> GetTankTypes()
        {
            var result = await _tankService.GetTankTypes();
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetMappedTankTypes(int projectId, string? vesseltype)
        {
            var result = await _tankService.GetMappedTankTypes(projectId, vesseltype);

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            return Ok(result.Data);
        }



        [HttpGet("[action]")]
        public async Task<IActionResult> GetVesselDetails()
        {
            var result = await _tankService.GetVesselDetails();
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetIMONumberByVesselName(string vesselName)
        {
            var result = await _tankService.GetIMONumberByVesselName(vesselName);
            return Ok(result.Data);
        }


        [HttpGet("[action]")]
        public async Task<IActionResult> UpdateTank(string tankId, string vesseltype, string vesselname, string imonumber, string tanktype, string tankname, bool isActive)
        {
            var result = await _tankService.UpdateTank(tankId, vesseltype, vesselname, imonumber, tanktype, tankname, isActive);
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetVesselType()
        {
            var result = await _tankService.GetVesselType();
            return Ok(result.Data);
        }


        [HttpGet("[action]")]
        public async Task<IActionResult> GetShipType()
        {
            var result = await _tankService.GetShipType();
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetVesselTypeList()
        {
            var result = await _tankService.GetVesselTypeList();
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> FilterMenuCustomization_Read([FromQuery] DataSourceRequest request)
        {
            try
            {
                var serviceResult = await _tankService.FilterMenuCustomization_Read();

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
                return StatusCode(500, new { message = "Error retrieving tanks", error = ex.Message });
            }
        }


        [HttpGet("[action]")]
        public async Task<IActionResult> ManageTankFilter_TankName(string IMO)
        {
            var result = await _tankService.ManageTankFilter_TankName(IMO);
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> ManageTankFilter_VesselType(string IMO)
        {
            var result = await _tankService.ManageTankFilter_VesselType(IMO);
            return Ok(result.Data);
        }


        [HttpGet("[action]")]
        public async Task<IActionResult> ManageTankFilter_VesselName(string IMO)
        {
            var result = await _tankService.ManageTankFilter_VesselName(IMO);
            return Ok(result.Data);
        }



        [HttpGet("[action]")]
        public async Task<IActionResult> ManageTankFilter_IMONumber(string IMO)
        {
            var result = await _tankService.ManageTankFilter_IMONumber(IMO);
            return Ok(result.Data);
        }


        [HttpGet("[action]")]
        public async Task<IActionResult> ManageTankFilter_TankType(string IMO)
        {
            var result = await _tankService.ManageTankFilter_TankType(IMO);
            return Ok(result.Data);
        }
        [HttpGet("[action]")]
        public async Task<IActionResult> ManageTankFilter_Project(string IMO)
        {
            var projectnames = _tankService.ManageTankFilter_Project(IMO).Result;
            return Ok(projectnames.Data);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ManageTankActiveCheckBox([FromBody] ManageTankStatusRequest request)
        {
            var result = await _tankService.ManageTankActiveCheckBox(request.Data, request.Status, request.IMO);

            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> ManageTankInActiveCheckBox()
        {
            var data = new
            {
                status = "Inactive"
            };

            return Ok("Inactive");
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> DeleteTanks(Guid tankId, string? IMO = null, int ProjectId = 0)
        {
            var result = await _tankService.DeleteTanks(tankId, IMO, ProjectId);
            return Ok(result.Data);
        }

    }
}
