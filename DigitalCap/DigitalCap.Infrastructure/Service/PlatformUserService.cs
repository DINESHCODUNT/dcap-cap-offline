using DigitalCap.Core.Enumerations;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.WebApi.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace DigitalCap.Infrastructure.Service
{
    public class PlatformUserService : IPlatformUserService
    {
        private readonly IPlatformUserRepository _platformUserrepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContext;

        public PlatformUserService(
            IPlatformUserRepository platformUserrepository,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor)
        {
            _platformUserrepository = platformUserrepository;
            _userManager = userManager;
            _httpContext = httpContextAccessor;
        }

        private async Task<string> GetLoggedInUserName()
        {
            var principal = _httpContext.HttpContext.User as ClaimsPrincipal;
            var userId = _userManager.GetUserId(principal);
            if (userId == null) return string.Empty;

            var user = await _userManager.FindByIdAsync(userId);
            return user.UserName;
        }

        private string GetLoggedInUserId()
        {
            //var principal = _httpContext.HttpContext.User as ClaimsPrincipal;
            return _userManager.GetUserId(_httpContext.HttpContext.User);
        }

        //check this & fix it properly

        //public async Task<IReadOnlyList<UserWCNMappingModel>> AddEditUserWCNMappingAsync(string email, List<UserWCNMappingModel> mappings)
        //{
        //    if (string.IsNullOrWhiteSpace(email))
        //        throw new ArgumentException("Email is required", nameof(email));

        //    if (mappings == null || !mappings.Any())
        //        return Array.Empty<UserWCNMappingModel>();

        //    foreach (var item in mappings)
        //    {
        //        await _platformUserrepository.AddEditUserWCNMappingAsync(email, item.CompanyName, item.WCN);
        //    }

        //    return mappings;
        //}


        public async Task<List<UserWCNMappingModel>> GetWCNGrid()
        {
            var email = await GetLoggedInUserName();
            return (List<UserWCNMappingModel>)await _platformUserrepository.GetWCNGridAsync(email);
        }

        public async Task InitialLoginActions()
        {
            var email = await GetLoggedInUserName();
            await _platformUserrepository.PerformInitialLoginAsync(email);
        }

        public async Task<bool> AcceptTermsOfUse(UserModel userModel, int applicationId)
        {
            var loggedInUserName = await GetLoggedInUserName();
            return await _platformUserrepository.AcceptTermsOfUseAsync(userModel, applicationId);
        }

        public async Task<string> GetTermsOfUse(int applicationId)
        {
            return await _platformUserrepository.GetTermsOfUseTextAsync(applicationId);
        }

        //public async Task<UserModel> GetUserAsync(int appId, string loggedInUserName, string loggedInUserId)
        //{
        //    if (string.IsNullOrWhiteSpace(loggedInUserName))
        //        throw new ArgumentException("User name is required");

        //    if (string.IsNullOrWhiteSpace(loggedInUserId))
        //        throw new ArgumentException("User id is required");

        //    return await _platformUserrepository.GetUser(
        //        appId,
        //        loggedInUserName,
        //        loggedInUserId);
        //}



        public async Task<IEnumerable<UserType>> GetUserType()
        {
            var userId = GetLoggedInUserId();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return new List<UserType>();

            var roles = await _userManager.GetRolesAsync(user);
            return roles.Select(x => (UserType)Enum.Parse(typeof(UserType), x.ToUpper())).ToList();
        }

        public async Task<IEnumerable<UserType>> GetUserTypeByPrefix(string prefix)
        {
            var userId = GetLoggedInUserId();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return new List<UserType>();

            var roles = await _userManager.GetRolesAsync(user);
            return roles.Where(x => x.StartsWith(prefix)).Select(x => (UserType)Enum.Parse(typeof(UserType), x)).ToList();
        }

        public async Task<bool> AddNewWCNAsync(WCNModel model)
        {
            var email = await GetLoggedInUserName();
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            return await _platformUserrepository.AddNewWCNAsync(
             email, model);
        }


        //public async Task<bool> AddNewWCN(WCNModel wCNModel)
        //{
        //    var loggedInUserName = await GetLoggedInUserName();
        //    return await _platformUserrepository.AddNewWCN(wCNModel, loggedInUserName);
        //}

        public Task InitialLoginActions(string email)
        {
            throw new NotImplementedException();
        }
    }
}
