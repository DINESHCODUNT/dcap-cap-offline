using DigitalCap.Core.Enumerations;
using DigitalCap.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DigitalCap.Core.Interfaces.Service
{
    public interface IPlatformUserService
    {
        //Task<IReadOnlyList<UserWCNMappingModel>> AddEditUserWCNMappingAsync(string email, List<UserWCNMappingModel> mappings);

        //Task<List<UserWCNMappingModel>> AddEditUserWCNMapping(List<UserWCNMappingModel> mappings);

        Task<List<UserWCNMappingModel>> GetWCNGrid();

        Task InitialLoginActions(string email);

        Task<bool> AcceptTermsOfUse(UserModel userModel, int applicationId);

        Task<string> GetTermsOfUse(int applicationId);

        //    Task<UserModel> GetUserAsync(int appId, string loggedInUserName, string loggedInUserId);

        Task<IEnumerable<UserType>> GetUserType();

        Task<IEnumerable<UserType>> GetUserTypeByPrefix(string prefix);

        Task<bool> AddNewWCNAsync(WCNModel wCNModel);
    }
}
