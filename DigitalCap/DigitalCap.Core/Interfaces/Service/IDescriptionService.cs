using DigitalCap.Core.DTO;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.Grading;
using DigitalCap.Core.Models.ImageDescription;
using DigitalCap.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DigitalCap.Core.Interfaces.Service
{
    public interface IDescriptionService
    {
        Task<ServiceResult<PagedResult<ImageDescriptions>>> GetAllAsync(int pageNumber, int pageSize);
       
        Task<ImageDescriptions> GetByIdAsync(int id);
        Task<ServiceResult<bool>> CreateAsync(ImageDescriptionDTO model);
        Task<ServiceResult<ImageDescriptionViewModel>> UpdateAsync(ImageDescriptionViewModel model);
        Task<ServiceResult<List<GradingSection>>> GetSectionNamesByTemplateNameAndVesselTypeAsync(string templateName, string vesselType);

        Task<ServiceResult<bool>> EditDescriptionAsync(ImageDescriptionViewModel model);
        Task<ServiceResult<GetDescriptionForEditResponse>> GetDescriptionForEditAsync(int descriptionId, Guid sectionId, int tankTypeId, string username);

        Task<ServiceResult<bool>> AddNewDescriptionAsync(ImageDescriptionViewModel model);
        Task<ServiceResult<GetDescriptionForAddResponse>> GetDescriptionForAddAsync(string username);
        Task<ServiceResult<bool>> SetManageDescriptionFiltersAsync(ManageDescriptionFilterRequest request);
        Task<ServiceResult<ManageDescriptionResponse>> ManageDescriptionAsync(string username, bool isActive, int descriptionRestoreFilter, int searchDescriptionRestoreFilter, int pageNumber, int pageSize);
        //Task<ServiceResult<List<string>>> GetDistinctVesselTypesAsync();
        Task<ServiceResult<AddDescriptionMetaDTO>>GetAddDescriptionMetadataAsync(string userName);

    }
}
