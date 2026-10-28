using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Infrastructure.Service;
using ElectronNET.API;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace DigitalCap.WebApi.Filters
{
    /// <summary>
    /// Desktop app, online: the API works on the live database, but a project downloaded on this computer must
    /// only be edited offline (its local copy is synced later and would overwrite/conflict with live edits).
    /// Edit requests (POST/PUT/PATCH/DELETE, see <see cref="OnlineReadOnlyProjectRules"/>) for such a project are
    /// answered with 409 and {success:false, message}. Registered globally in Program.cs.
    /// </summary>
    public class OnlineReadOnlyProjectFilter : IAsyncActionFilter
    {
        private readonly IOfflineTransferRepository _offlineTransferRepository;
        private readonly ILogger<OnlineReadOnlyProjectFilter> _logger;

        public OnlineReadOnlyProjectFilter(IOfflineTransferRepository offlineTransferRepository, ILogger<OnlineReadOnlyProjectFilter> logger)
        {
            _offlineTransferRepository = offlineTransferRepository;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!HybridSupport.IsElectronActive || !IsOnline.OnlineOrOffline)
            {
                await next();
                return;
            }

            var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
            if (!OnlineReadOnlyProjectRules.IsGuardedRequest(context.HttpContext.Request.Method, descriptor?.ControllerName, descriptor?.ActionName)
                || !OnlineReadOnlyProjectRules.TryGetProjectId(descriptor?.ControllerName, context.ActionArguments, out var projectId))
            {
                await next();
                return;
            }

            bool isLocal;
            try
            {
                isLocal = await _offlineTransferRepository.ProjectExistsAsync(projectId, context.HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                // Without the local database we cannot tell; refuse rather than risk an edit that conflicts with the sync.
                _logger.LogError(ex, "Read-only check: reading the local database for project {ProjectId} failed", projectId);
                context.Result = new ObjectResult(new { success = false, message = "Could not check whether this project is downloaded on this computer: " + ex.Message })
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable
                };
                return;
            }

            if (isLocal)
            {
                _logger.LogInformation("Blocked online edit {Controller}.{Action} of locally downloaded project {ProjectId}",
                    descriptor?.ControllerName, descriptor?.ActionName, projectId);
                context.Result = new ObjectResult(new { success = false, message = OnlineReadOnlyProjectRules.ConflictMessage })
                {
                    StatusCode = StatusCodes.Status409Conflict
                };
                return;
            }

            await next();
        }
    }
}
