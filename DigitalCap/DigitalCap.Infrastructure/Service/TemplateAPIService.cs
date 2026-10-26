using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalCap.Infrastructure.Service
{
    public class TemplateAPIService : ITemplateAPIService
    {
        private readonly ITemplateService _templateService;

        public TemplateAPIService(ITemplateService templateService)
        {
            _templateService = templateService;
        }

        //public async Task<ServiceResult<byte[]>> PopulateTemplate(int projectId)
        //{
        //    try
        //    {
        //        var result = await _templateService.PopulateTemplate(projectId);
        //        return ServiceResult<byte[]>.Success(result);
        //    }
        //    catch (Exception ex)
        //    {
        //        return ServiceResult<byte[]>.Failure("Failed to populate template");
        //    }
        //}
    }
}
