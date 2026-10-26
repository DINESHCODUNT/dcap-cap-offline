using Azure.Core;
using DigitalCap.Core.Configuration;
using DigitalCap.Core.DTO;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.View.Admin;
using DigitalCap.Core.Security;
using DigitalCap.Core.Security.Extensions;
using DigitalCap.Core.ViewModels.AccountViewModels;
using DigitalCap.WebApi.Core;
//using Microsoft.AspNet.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;



namespace DigitalCap.Infrastructure.Service
{
    public class UserAccountService : IUserAccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IUserAccountRepository _userAccountRepository;
        private readonly ISecurityService _securityService;
        private readonly ISecurityClientService _securityClientService;
        private readonly ISecurityClientRepository _securityClientRepository;
        private readonly IPlatformUserService _platformUserService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserAccountService> _logger;

        public UserAccountService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IUserAccountRepository userAccountRepository,
            ISecurityService securityService,
            ISecurityClientService securityClientService,
            ISecurityClientRepository securityClientRepository,
            IPlatformUserService platformUserService,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<UserAccountService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _userAccountRepository = userAccountRepository;
            _securityService = securityService;
            _securityClientService = securityClientService;
            _securityClientRepository = securityClientRepository;
            _platformUserService = platformUserService;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        // ---------------- AUTH ----------------

        public async Task<ServiceResult<LoginResult>> LoginAsync(LoginViewModel model)
        {
            if (model == null)
                return ServiceResult<LoginResult>.Failure("Invalid request");

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return ServiceResult<LoginResult>.Failure("Invalid login attempt");

            var account = await _userAccountRepository.GetByAspNetIdAsync(user.Id);
            if (account == null || account.Deleted)
                return ServiceResult<LoginResult>.Failure("Invalid login attempt");

            var signInResult = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (signInResult.Succeeded)
            {
                _logger.LogInformation("User logged in");

                return ServiceResult<LoginResult>.Success(new LoginResult
                {
                    Success = true,
                    Message = "Login successful"
                });
            }

            if (signInResult.IsLockedOut)
            {
                return ServiceResult<LoginResult>.Failure("User account is locked");
            }

            if (signInResult.IsNotAllowed)
            {
                return ServiceResult<LoginResult>.Failure("Login not allowed");
            }

            return ServiceResult<LoginResult>.Failure("Invalid login attempt");
        }
        public async Task<ServiceResult<string>> LogoutAsync()
        {
            await _signInManager.SignOutAsync();

            _logger.LogInformation("User logged out");

            return ServiceResult<string>.Success("Logout successful");
        }

        //  EMAIL / PASSWORD 
        public async Task<ServiceResult<string>> ConfirmEmailAsync(string userId, string code)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return ServiceResult<string>.Failure("User not found");

            var result = await _userManager.ConfirmEmailAsync(user, code);

            if (result.Succeeded)
            {
                return ServiceResult<string>.Success("Email confirmed");
            }

            return ServiceResult<string>.Failure("Email confirmation failed");
        }

        public async Task<ServiceResult<string>> ForgotPasswordAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return ServiceResult<string>.Failure("Email is required");

            var user = await _userManager.FindByEmailAsync(email);


            if (user == null)
            {
                var u = new ApplicationUser
                {
                    Email = email,
                    UserName = email,
                    NormalizedEmail = email.ToUpper(),
                    NormalizedUserName = email.ToUpper(),
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString()
                };
                var result = await _userManager.CreateAsync(u);
                
                if (!result.Succeeded)
                {
                    return ServiceResult<string>.Failure("Failed to create provisional account.");
                }

                user = await _userManager.FindByEmailAsync(email);
            }

            if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
            {
                return ServiceResult<string>.Success(
                    "If the email is registered, a password reset link has been sent.");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var url =
                $"{_configuration["Frontend:ResetPasswordUrl"]}" +
                $"?email={Uri.EscapeDataString(email)}" +
                $"&token={Uri.EscapeDataString(token)}";

            try
            {
                await _emailService.SendEmailAsync(
                    email,
                    "Reset Password",
                    $"Reset your password using this link:\n{url}");
            }
            catch (Exception ex)
            {
               
                _logger.LogError(ex, "Failed to send forgot password email to {Email}", email);
            }

            return ServiceResult<string>.Success(
                "If the email is registered, a password reset link has been sent.");
        }

        public async Task<ServiceResult<string>> ResetPasswordAsync(string email, string token, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(newPassword))
            {
                return ServiceResult<string>.Failure("Invalid reset request");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return ServiceResult<string>.Failure("Invalid reset request");
            }

            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
            {
                return ServiceResult<string>.Success("Password reset successful");
            }

            var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            return ServiceResult<string>.Failure(errors);
        }

        public async Task<ServiceResult<string>> ExternalLoginAsync(string? returnUrl, string? remoteError)
        {
            if (!string.IsNullOrWhiteSpace(remoteError))
            {
                return ServiceResult<string>.Failure(remoteError);
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return ServiceResult<string>.Failure("External login information not found");
            }

            var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);

            if (user == null)
            {
                user = await HandleFirstTimeExternalLogin(info);
                if (user == null)
                {
                    return ServiceResult<string>.Failure("User registration failed");
                }
            }

            await ValidateUserAccount(user.Id);

            await _signInManager.SignInAsync(user, isPersistent: false);

            return ServiceResult<string>.Success(returnUrl);
        }

        private async Task<ApplicationUser?> HandleFirstTimeExternalLoginAsync(ExternalLoginInfo info)
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrWhiteSpace(email))
                return null;

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return null;

            var result = await _userManager.AddLoginAsync(user, new UserLoginInfo(
            info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));

            if (!result.Succeeded)
                return null;

            await _platformUserService.InitialLoginActions(email);
            await NotifyAdmins(email);

            return user;
        }

        private async Task<ApplicationUser> HandleFirstTimeExternalLogin(ExternalLoginInfo info)
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
                throw new UnauthorizedAccessException("Email not provided");

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                throw new UnauthorizedAccessException("User not registered");

            var result = await _userManager.AddLoginAsync(user,
                new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName));

            if (!result.Succeeded)
                throw new InvalidOperationException("External login registration failed");

            await _platformUserService.InitialLoginActions(email);
            await NotifyAdmins(email);

            return user;
        }

        private async Task ValidateUserAccount(string aspNetUserId)
        {
            var account = await _userAccountRepository.GetByAspNetIdAsync(aspNetUserId);
            if (account == null || account.Deleted)
                throw new UnauthorizedAccessException("Invalid account");
        }

        private async Task NotifyAdmins(string email)
        {
            var admins = _configuration["EmailAbsAdmin"]?.Split(',');
            if (admins == null) return;

            foreach (var admin in admins)
            {
                await _emailService.SendEmailAsync(
                    admin,
                    "New User Registered",
                    $"{email} has registered and needs role assignment.");
            }
        }

        //  ROLES & PERMISSIONS 

        public async Task<bool> AssignRolesAndPermissionsAsync(Guid userId, IEnumerable<string> roles, IEnumerable<string> permissions)
        {
            var userDto = await _securityService.GetUser(userId);
            if (userDto == null)
                return false;

            if (!await _securityService.UpdateRolesForUser(
                    userDto,
                    roles?.ToArray() ?? Array.Empty<string>()))
                return false;

            return await _securityService.UpdatePermissionsForUser(
                userId,
                permissions?.ToPermissions().ToArray());
        }

        //ACCOUNT

        public async Task<CreateAccountResponse> GetCreateAccountDataAsync()
        {
            var result = await _securityClientService.GetAllClientsAsync();
            return new CreateAccountResponse
            {
                Clients = result.IsSuccess ? result.Data : new List<ClientModel>().AsReadOnly()
            };
        }


        public async Task<object?> GetEditAccountDataAsync()
        {
            var user = await _securityService.GetCurrentUserAsync();
            if (user == null) return null;

            return new
            {
                aspNetUser = user,
                userAccount = user.Data.UserAccount,
                clients = _securityClientRepository.GetAll().ToList()
            };
        }


        public async Task<Guid> CreateAccountAsync(CreateAccountRequest request)
        {
            //var user = request.AspNetUser;
            //user.UserName = user.Email;
            ////user.EmailConfirmed = true;
            ///
            var user = new ApplicationUser
            {
                Email = request.Email,
                UserName = request.Email
            };

            await _userManager.CreateAsync(user, request.Password);

            request.UserAccount.AspNetUserId = user.Id;
            _userAccountRepository.Insert(request.UserAccount);
            _userAccountRepository.Commit();

            return Guid.Parse(user.Id);
        }


        //public async Task<Guid> EditAccountAsync(AccountViewModel model)
        //{
        //    if (model == null)
        //        throw new ArgumentNullException(nameof(model));

        //    var user = await _userManager.FindByIdAsync(model.UserId);
        //    if (user == null)
        //        throw new InvalidOperationException("User not found");

        //    user.Email = model.Email;
        //    user.UserName = model.Email;
        //    user.IsEnabled = model.IsEnabled;

        //    var result = await _userManager.UpdateAsync(user);
        //    if (!result.Succeeded)
        //        throw new InvalidOperationException(
        //            string.Join(" | ", result.Errors.Select(e => e.Description)));

        //    await SaveAsync(model.UserAccount);
        //    await CommitAsync();

        //    return Guid.Parse(user.Id);
        //}
        public async Task<ServiceResult<AccountViewModel>> EditAccount(AccountViewModel accountVM)
        {
            var account = accountVM.UserAccount;

            var existingUser = await _userManager.FindByEmailAsync(accountVM.Email);

            ApplicationUser user;

            if (existingUser != null)
            {
                user = existingUser;

                if (!user.IsEnabled && Guid.TryParse(user.Id, out var userId))
                {
                    await _securityService.ToggleUserIsEnabled(userId, true);
                }

                user.UserName = user.Email;
                user.EmailConfirmed = true;

                var updateResult = await _userManager.UpdateAsync(user);


                account.AspNetUserId = user.Id;
            }
            else
            {
                user = new ApplicationUser
                {
                    Email = accountVM.Email,
                    UserName = accountVM.Email,
                    EmailConfirmed = true,
                    IsEnabled = true
                };

                var createResult = await _userManager.CreateAsync(user, accountVM.Password);
                if (!createResult.Succeeded)
                {
                    var error = string.Join(". ", createResult.Errors.Select(x => x.Description));
                    return ServiceResult<AccountViewModel>.Failure($"Failed to create account. Details: {error}");
                }

                account.AspNetUserId = user.Id;
            }

            var client = _securityClientRepository.GetAll().FirstOrDefault();
            if (client != null)
                account.ClientId = client.Id;

            var existingAccount =
                await _userAccountRepository.GetByAspNetId(account.AspNetUserId);

            if (existingAccount == null)
                _userAccountRepository.Insert(account);
            else
            {
                account.Id = existingAccount.Id;
                _userAccountRepository.Update(account);
            }

            _userAccountRepository.Commit();

            // 5️⃣ Permissions / Roles logic
            var currentUserResult = await _securityService.GetCurrentUserAsync();
            //if (!currentUserResult.Succeeded)
            //    return Unauthorized();

            var currentUser = currentUserResult.Data;

            if (currentUser.IsABSAdministrator()
                && !currentUser.HasAllPermissions(Permission.SystemAdministrator))
            {
                await _userManager.AddClaimAsync(user, Permission.SystemAdministrator.ToClaim());
            }
            else
            {
                var viewOnlyRole = _securityService.Roles.FirstOrDefault(r => r == DigitalCap.Core.Constants.AppConstants.Roles.ClientAuthorizedThirdParties);

                if (viewOnlyRole != null && Guid.TryParse(user.Id, out var uid))
                {
                    // Get ApplicationDto for UpdateRolesForUser
                    var userDto = await _securityService.GetUser(uid);
                    if (userDto != null)
                    {
                        await _securityService.UpdateRolesForUser(userDto, viewOnlyRole);
                    }
                }
            }

            return ServiceResult<AccountViewModel>.Success(accountVM);

        }
        public async Task RequestAccessAsync(string email)
        {
            string to = _configuration.GetRequestAccessEmail() + "," + email;

            await _emailService.SendEmailAsync(
                to,
                "Grant User Access to CAP",
                "User is requesting access"
            );
        }

        //  PROFILE 
        //public async Task<UserProfileResponse> GetProfileAsync()
        //{
        //    var user = await _securityService.GetCurrentUser();
        //    var account = await _userAccountRepository.GetByAspNetIdAsync(user.Id);

        //    return new UserProfileResponse
        //    {
        //        User = user,
        //        Account = account
        //    };
        //}

        //public async Task UpdateProfileAsync(EditProfileRequest request)
        //{
        //    await _userManager.UpdateAsync(request.AspNetUser);
        //    await SaveAsync(request.UserAccount);
        //    await CommitAsync();
        //}

        public async Task<IEnumerable<string>> GetUserRolesAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
                return Enumerable.Empty<string>();

            return await _userManager.GetRolesAsync(user);
        }

        public async Task<IEnumerable<string>> GetUserPermissionsAsync(Guid userId)
        {
            return await _userAccountRepository.GetPermissionsForUserAsync(userId);
        }

        public Task<UserAccountModel?> GetByAspNetIdAsync(string aspNetUserId)
        {
            return _userAccountRepository.GetByAspNetIdAsync(aspNetUserId);
        }

        public Task<UserAccountModel?> GetByAspNetIdIncludingDeletedAsync(string aspNetUserId)
            => _userAccountRepository.GetByAspNetIdIncludingDeletedAsync(aspNetUserId);

        public Task<IEnumerable<UserAccountModel>> GetUsersInRoleAsync(string roleName)
            => _userAccountRepository.GetUsersInRoleAsync(roleName);

        public Task<IEnumerable<UserAccountModel>> GetUsersInRoleForClientAsync(string roleName, Guid clientId)
            => _userAccountRepository.GetUsersInRoleForClientAsync(roleName, clientId);

        public Task<IEnumerable<UserAccountModel>> GetUsersWithPermissionAsync(Permission permission)
            => _userAccountRepository.GetUsersWithPermissionAsync(permission);

        public Task<IEnumerable<CapUser>> GetAbsUsersAsync()
            => _userAccountRepository.GetAbsUsersAsync();

        public Task<IEnumerable<CapUser>> GetClientUsersAsync(string clientId)
            => _userAccountRepository.GetClientUsersAsync(clientId);

        public async Task<UserAccountModel> SaveAsync(UserAccountModel account)
        {
            var existing = await _userAccountRepository.GetByAspNetIdActiveOrDeletedAsync(account.AspNetUserId);

            //var existing = existingList.FirstOrDefault();

            if (existing == null)
            {
                _userAccountRepository.Insert(account);
            }
            else
            {
                account.Id = existing.Id;
                _userAccountRepository.Update(account);
            }

            return account;
        }



        public async Task CommitAsync()
        {
            await Task.Run(() => _userAccountRepository.Commit());
        }

        public async Task<ManageAbsUsersResponseDto> GetManageAbsUsersAsync(bool userUpdated, bool userCreated)
        {
            var users = await _userAccountRepository.GetAbsUsers();

            return new ManageAbsUsersResponseDto
            {
                CapUsers = users,
                UserUpdated = userUpdated,
                UserCreated = userCreated,
                Company = DigitalCap.Core.Constants.AppConstants.ABS_Company_Name
            };
        }

        public async Task<ServiceResult<bool>> DeleteAsync(string aspNetUserId)
        {
            try
            {
                var user = await _userAccountRepository.GetByAspNetIdAsync(aspNetUserId);

                if (user == null)
                {
                    return ServiceResult<bool>.Failure("User not found.");
                }

                await _userAccountRepository.DeleteAsync(user.Id); 
                _userAccountRepository.Commit();

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Failure($"Error deleting user: {ex.Message}");
            }
        }

        public async Task<ServiceResult<CapUsersViewModel>> ManageAbsUsers(bool userUpdated = false, bool userCreated = false)
        {
            try
            {
                var capUsersViewModel = new CapUsersViewModel
                {
                    CapUsers = await _userAccountRepository.GetAbsUsersAsync(),
                    UserUpdated = userUpdated,
                    UserCreated = userCreated,
                    Company = DigitalCap.Core.Constants.AppConstants.ABS_Company_Name,
                    IsClientUsers = false
                };

                return ServiceResult<CapUsersViewModel>.Success(capUsersViewModel);
            }
            catch (Exception ex)
            {
                return ServiceResult<CapUsersViewModel>.Failure($"Internal server error: {ex.Message}");
            }
        }

        public async Task<ServiceResult<CapUsersViewModel>> ManageClientUsersAsync(bool userUpdated = false, bool userCreated = false)
        {
            try
            {
                var currentUser = await _securityService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return ServiceResult<CapUsersViewModel>.Failure("Current user not found");
                }

                var currentUserAccount = await _userAccountRepository.GetByAspNetIdAsync(currentUser.Data.Id);
                if (currentUserAccount == null)
                {
                    return ServiceResult<CapUsersViewModel>.Failure("User account not found");
                }

                var clientUsers = await _userAccountRepository.GetClientUsersAsync(currentUserAccount.ClientId.ToString());
                var capUsersViewModel = new CapUsersViewModel
                {
                    CapUsers = clientUsers,
                    UserUpdated = userUpdated,
                    UserCreated = userCreated,
                    IsClientUsers = true,
                    Company = clientUsers.FirstOrDefault()?.Company ?? string.Empty,
                    ClientId = currentUserAccount.ClientId
                };

                return ServiceResult<CapUsersViewModel>.Success(capUsersViewModel);
            }
            catch (Exception ex)
            {
                return ServiceResult<CapUsersViewModel>.Failure($"Internal server error: {ex.Message}");
            }
        }
    }
}

