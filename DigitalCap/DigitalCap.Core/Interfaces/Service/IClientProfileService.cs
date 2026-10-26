using DigitalCap.Core.DTO;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.View.Admin;
using DigitalCap.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static DigitalCap.Core.DTO.ClientProfileDTO;

namespace DigitalCap.Core.Interfaces.Service
{
    public interface IClientProfileService
    {
        Task<ServiceResult<ClientProfileViewModel>> GetClientProfileByIdAsync(Guid clientId);
        Task<ServiceResult<List<ClientSummaryDto>>> GetClientSummariesAsync();
        Task<ServiceResult<ClientProfileViewModel>> CreateClientProfileAsync(ClientProfileViewModel model);
        Task<ServiceResult<Guid>> EditClientProfileAsync(ClientProfileViewModel model);
        Task<ServiceResult<ClientProfileDetailDto>> GetClientProfileWithDetailsAsync(Guid clientId, bool freshmeat = false);
        Task<ServiceResult<ClientProfileViewModel>> AddClientProfileAsync(ClientProfileViewModel model);
        Task<ServiceResult<AddClientAdminInitDto>> GetAddClientAdminInitAsync(Guid clientId, string company);
       // Task<ServiceResult<bool>> CreateClientAdminAsync(CreateClientAdminDto dto);
        //Task<ServiceResult<bool>> SendNewUserEmailAsync(ApplicationUser user, UserAccountModel account, string password, object email);
        Task<ServiceResult<Guid>> SaveClientProfileAsync(ClientProfileViewModel model, bool freshmeat);
        Task<ServiceResult<bool>> DeleteClientProfileAsync(Guid Id);
        Task<ServiceResult<bool>> DeleteClientAdminAsync(string aspNetUserId);
        Task<ClientSummaryDto> GetClientProfileAsync(Guid clientId);
        Task<RetrieveCompanyNameResponseDto> RetrieveCompanyNameAsync(uint wcn);
        Task<ServiceResult<bool>> StoreClientAdminAsync(CreateClientAdminRequest request);
        //Task<ServiceResult<bool>> StoreClientAdminAsync(AccountViewModel model);


        //For Add ClientAdmin
       
        Task<ServiceResult<AddClientAdminResponse>> GetAddClientAdminData(Guid clientId, string companyName);
        Task<ServiceResult<bool>> AddClientAdminAsync(CreateClientAdminRequest request);

    }
}
