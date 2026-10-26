using DigitalCap.Core.DTO;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.View.Admin;
using DigitalCap.Core.Security;
using DigitalCap.Core.ViewModels.AccountViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DigitalCap.Core.Interfaces.Service
{
    public interface IUserAccountService
    {
        Task<ServiceResult<LoginResult>> LoginAsync(LoginViewModel model);
        Task<ServiceResult<string>> LogoutAsync();
        Task<ServiceResult<string>> ConfirmEmailAsync(string userId, string code);
        Task<ServiceResult<string>> ForgotPasswordAsync(string email);
        Task<ServiceResult<string>> ResetPasswordAsync(string email, string token, string newPassword);
        Task<bool> AssignRolesAndPermissionsAsync(Guid userId, IEnumerable<string> roles, IEnumerable<string> permissions);

        Task<CreateAccountResponse> GetCreateAccountDataAsync();
        Task<object?> GetEditAccountDataAsync();

        Task<IEnumerable<string>> GetUserPermissionsAsync(Guid userId);
        Task<IEnumerable<string>> GetUserRolesAsync(Guid userId);

        Task<Guid> CreateAccountAsync(CreateAccountRequest request);
       // Task<Guid> EditAccountAsync(AccountViewModel model);
        Task<ServiceResult<AccountViewModel>> EditAccount(AccountViewModel accountVM);

        //Task<IEnumerable<ManagedUserResponse>> GetManagedUsersAsync();

        //Task RequestAccessAsync(string email);
        //Task ChangePasswordAsync(ChangePasswordRequest request);

        // Task<UserProfileResponse> GetProfileAsync();
        // Task UpdateProfileAsync(EditProfileRequest request);

        // Repository passthroughs
        Task<UserAccountModel?> GetByAspNetIdAsync(string aspNetUserId);
        Task<UserAccountModel?> GetByAspNetIdIncludingDeletedAsync(string aspNetUserId);
        Task<IEnumerable<UserAccountModel>> GetUsersInRoleAsync(string roleName);
        Task<IEnumerable<UserAccountModel>> GetUsersInRoleForClientAsync(string roleName, Guid clientId);
        Task<IEnumerable<UserAccountModel>> GetUsersWithPermissionAsync(Permission permission);
        Task<IEnumerable<CapUser>> GetAbsUsersAsync();
        Task<IEnumerable<CapUser>> GetClientUsersAsync(string clientId);

        Task<UserAccountModel> SaveAsync(UserAccountModel account);
        //Task<IEnumerable<UserAccountModel>> GetByAspNetIdActiveOrDeletedAsync(string id);
        Task CommitAsync();
        Task<ServiceResult<string>> ExternalLoginAsync(string? returnUrl, string? remoteError);
        Task RequestAccessAsync(string email);
        
        
        //ManageAbsUsers
        Task<ManageAbsUsersResponseDto> GetManageAbsUsersAsync(bool userUpdated,bool userCreated);
        Task<ServiceResult<bool>> DeleteAsync(string aspNetUserId);
        //Task<ServiceResult<bool>> DeleteAsync(Guid id);
        Task<ServiceResult<CapUsersViewModel>> ManageAbsUsers(bool userUpdated = false, bool userCreated = false);
        
        //ManageClientUsers
        Task<ServiceResult<CapUsersViewModel>> ManageClientUsersAsync(bool userUpdated = false, bool userCreated = false);
    }
}
