using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Infrastructure.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DigitalCap.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CapController : ApiBaseController
    {
        private readonly ICapService _capService;
        private readonly IVesselService _vesselService;


        public CapController(ICapService capService, IVesselService vesselService)
        {
            _capService = capService;
            _vesselService = vesselService;
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetCapIndex()
        {
            var result = await _capService.GetCapIndexAsync();

            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetProjectByStatuses()
        {
            var result = await _capService.GetProjectByStatusesAsync();

            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetCertsByRegion()
        {
            var result = await _capService.GetCertsByRegionAsync();

            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetProjectCount()
        {
            var result = await _capService.GetProjectCountAsync();

            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetCertsCount()
        {
            var result = await _capService.GetCertsCountAsync();

            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetSidebar()
        {
            var result = await _vesselService.GetSidebarAsync();

            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result);
        }
    }
}
