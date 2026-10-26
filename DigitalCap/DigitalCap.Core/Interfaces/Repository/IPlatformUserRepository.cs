using DigitalCap.Core.Enumerations;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DigitalCap.Core.Models;

namespace DigitalCap.Core.Interfaces.Repository
{

    public interface IPlatformUserRepository
    {
        Task<IEnumerable<UserType>> GetUserType(string objectId);
        Task DeleteUserWCNMappingAsync(string email);

        Task AddEditUserWCNMappingAsync(string email, string companyName, UserWCNMappingModel model);

        Task<IEnumerable<UserWCNMappingModel>> GetWCNGridAsync(string email);

        Task PerformInitialLoginAsync(string email);

        Task<string?> GetTermsOfUseTextAsync(int applicationId);
        Task<bool> AcceptTermsOfUseAsync(UserModel userModel, int applicationId);
        Task<UserModel?> GetUserTermsAsync(string userId, int applicationId);

        Task<bool> AddNewWCNAsync(string email, WCNModel model);
      //  Task<UserModel> GetUser(int appId, string loggedInUserName, string loggedInUserId);
        //  Task AddEditUserWCNMappingAsync(string email, string companyName, int wCN);
        //   Task<bool> AcceptTermsOfUseAsync(UserModel userModel, int applicationId, string loggedInUserName);
        // Task<UserModel> GetUser(int appId, string loggedInUserName, string userId);
    }
}






