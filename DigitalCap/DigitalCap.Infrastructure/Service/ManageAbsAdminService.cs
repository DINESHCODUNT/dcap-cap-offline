using DigitalCap.Core.Constants;
using DigitalCap.Core.DTO;
using DigitalCap.Core.Helpers.Constants;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.View.Admin;
using DigitalCap.Core.Security;


using DigitalCap.WebApi.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Cryptography;

namespace DigitalCap.Infrastructure.Service
{
    public class ManageAbsAdminService : IManageAbsAdminService
    {
        private readonly IUserAccountRepository _userAccountRepository;
        private readonly ISecurityService _securityService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly ITemplateReader _templateReader;

        public ManageAbsAdminService(
            IUserAccountRepository userAccountRepository,
            ISecurityService securityService,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            ITemplateReader templateReader)
        {
            _userAccountRepository = userAccountRepository;
            _securityService = securityService;
            _userManager = userManager;
            _emailService = emailService;
            _templateReader = templateReader;
        }

        public async Task<ServiceResult<AccountViewModel>> GetAbsUserAsync(Guid userId)
        {
            var aspNetUser = await _securityService.GetUser(userId);
            if (aspNetUser == null)
                return ServiceResult<AccountViewModel>.Failure("User not found");

            var userAccount = await _userAccountRepository.GetByAspNetIdAsync(userId.ToString());

            var model = new AccountViewModel
            {
                UserAccount = userAccount,
                AspNetUser = aspNetUser.ToString(),
                Company = userAccount?.Company,
                AbsUser = true,
                UpdatedRole = aspNetUser.DisplayRole
            };

            return ServiceResult<AccountViewModel>.Success(model);
        }

        public async Task<ServiceResult<bool>> CreateAbsUserAsync(CreateAbsUserRequest model)
        {
            try
            {
                var currentUser = await _securityService.GetCurrentUserAsync();
                if (!currentUser.IsSuccess)
                    return ServiceResult<bool>.Failure("Unauthorized");

                var currentUserResult = currentUser.Data;

                foreach (var role in FormValues.ABSRoles)
                {
                    if (model.UpdatedRole == role &&
                        !currentUserResult.Permissions.Contains(Permission.ManageAbsUsers))
                    {
                        return ServiceResult<bool>.Failure("Not authorized to assign this role.");
                    }
                }

                string password = GenerateSecurePassword();
                var existingUser = await _userManager.FindByEmailAsync(model.Email);

                if (existingUser != null)
                {
                    return ServiceResult<bool>.Failure("This email address already exists. Please use a different email address.");
                }

                ApplicationUser user = new ApplicationUser
                {
                    Email = model.Email,
                    UserName = model.Email,
                    EmailConfirmed = true,
                    IsEnabled = true
                };

                var result = await _userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    var error = string.Join(". ", result.Errors.Select(e => e.Description));
                    return ServiceResult<bool>.Failure(error);
                }

                var account = model.UserAccount;
                account.AspNetUserId = user.Id;
                account.IsAgreeToPrivacy = true;

                var existingAccount =
            await _userAccountRepository.GetByAspNetIdActiveOrDeletedAsync(user.Id);

                if (existingAccount == null)
                    _userAccountRepository.Insert(account);
                else
                {
                    account.Id = existingAccount.Id;
                    _userAccountRepository.Update(account);
                }

                _userAccountRepository.Commit();

                // Get ApplicationDto for UpdateRolesForUser
                var userDto = await _securityService.GetUser(Guid.Parse(user.Id));
                if (userDto != null)
                {
                    await _securityService.UpdateRolesForUser(userDto, model.UpdatedRole);
                }

                if (existingUser == null)
                {
                    await _emailService.SendNewUserEmailAsync(
                        user.Email,
                        user.Email,
                        password,
                        currentUserResult.Email);
                }

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Failure("Error saving user.");
            }
        }


        //public async Task<ServiceResult<bool>> EditAbsUserAsync(AccountViewModel model)
        //{
        //    try
        //    {
        //        var currentUser = await _securityService.GetCurrentUserAsync();
        //        var account = model.UserAccount;


        //        //var user = await _securityService.GetUser(Guid.Parse(model.Id));
        //        var user = await _securityService.GetUser(model.Id );



        //        string password = string.Empty;
        //        bool sendEmail = false;

        //        if (user == null)
        //        {
        //            password = Guid.NewGuid().ToString("N")[^8..];

        //            user = new ApplicationUser
        //            {
        //                Email = model.Email,
        //                UserName = model.Email,
        //                EmailConfirmed = true,
        //                IsEnabled = true
        //            };

        //            var result = await _userManager.CreateAsync(user, password);
        //            if (!result.Succeeded)
        //            {
        //                var error = string.Join(". ", result.Errors.Select(x => x.Description));
        //                return ServiceResult<bool>.Failure(error);
        //            }

        //            sendEmail = true;
        //        }

        //        account.AspNetUserId = user.Id;
        //        account.IsAgreeToPrivacy = true;

        //        var existingAccount =
        //    await _userAccountRepository.GetByAspNetIdActiveOrDeletedAsync(user.Id);

        //        if (existingAccount == null)
        //            _userAccountRepository.Insert(account);
        //        else
        //        {
        //            account.Id = existingAccount.Id;
        //            _userAccountRepository.Update(account);
        //        }

        //        _userAccountRepository.Commit();

        //        await _securityService.UpdateRolesForUser(user.Id, model.UpdatedRole);

        //        if (sendEmail)
        //        {
        //            await _emailService.SendNewUserEmailAsync(
        //                user.Email,
        //                user.Email,
        //                password,
        //                currentUser.Email);
        //        }

        //        return ServiceResult<bool>.Success(true);
        //    }
        //    catch (Exception ex)
        //    {
        //        return ServiceResult<bool>.Failure("Error saving user");
        //    }
        //}



        //public async Task<ServiceResult<string>> EditAbsUserAsync(AccountViewModel model)
        //{
        //    try
        //    {
        //        string password = "";
        //        var currentUserResult = await _securityService.GetCurrentUserAsync();

        //    //var currentUserResult = await _securityService.GetCurrentUserAsync();

        //    if (!currentUserResult.IsSuccess)
        //        return ServiceResult<string>.Failure("Unauthorized");

        //    var currentUser = currentUserResult.Data;

        //    //foreach (var role in FormValues.ABSRoles)
        //    //{
        //    //    if (model.UpdatedRole == role &&
        //    //        !currentUser.Permissions.Contains(Permission.ManageAbsUsers))
        //    //    {
        //    //        return ServiceResult<string>.Failure("Not authorized to assign this role.");
        //    //    }
        //    //}


        //    var adminUser = await _userAccountRepository.GetByAspNetId(currentUser.Id);


        //    var account = model.UserAccount;

        //    // Get existing user using Id from ViewModel
        //    ApplicationDto? user = null;

        //    if (!string.IsNullOrEmpty(model.Id))
        //    {
        //        user = await _securityService.GetUser(Guid.Parse(model.Id));
        //    }

        //    if (user == null)
        //    {
        //        password = Guid.NewGuid().ToString("N")[^8..];

        //        var newUser = new ApplicationUser
        //        {
        //            Email = model.Email,
        //            UserName = model.Email,
        //            EmailConfirmed = true,
        //            IsEnabled = true
        //        };

        //        var result = await _userManager.CreateAsync(newUser, password);

        //        if (!result.Succeeded)
        //        {
        //            return ServiceResult<bool>.Failure("User creation failed.");
        //        }

        //        user = new ApplicationDto
        //        {
        //            Id = Guid.Parse(newUser.Id),
        //            Email = newUser.Email,
        //            IsEnabled = newUser.IsEnabled
        //        };



        //    // Get existing user using Id from ViewModel
        //    //ApplicationUser? user = null;

        //    //if (!string.IsNullOrEmpty(model.Id))
        //    //{
        //    //    user = await _securityService.GetUser(Guid.Parse(model.Id));
        //    //}

        //    //if (user == null)
        //    //{
        //    //    password = Guid.NewGuid().ToString("N").Substring(27);

        //    //    user = new ApplicationUser
        //    //    {
        //    //        Email = model.Email,
        //    //        UserName = model.Email,
        //    //        EmailConfirmed = true,
        //    //        IsEnabled = true
        //    //    };
        //    //}
        //    //    var account = model.UserAccount;
        //    //var user = await _securityService.GetUser(Guid.Parse(model.Id));

        //    ////var user = await _securityService.GetUser(Guid.Parse(model.AspNetUser.Id));
        //    //    if (user == null)
        //    //    {
        //    //        password = Guid.NewGuid().ToString().Substring(27);
        //    //        user = model.AspNetUser;
        //    //        user.UserName = model.Email;
        //    //    }

        //    user.EmailConfirmed = true;
        //        account.IsAgreeToPrivacy = true;

        //        var existingUser = await _userManager.FindByEmailAsync(model.Email);

        //        if (existingUser != null)
        //        {
        //            if (existingUser.Roles == null)
        //            {
        //                existingUser.Roles =
        //                    (IReadOnlyList<string>)await _userManager.GetRolesAsync(existingUser);
        //            }

        //            user.Id = existingUser.Id;
        //            user.Roles = existingUser.Roles;

        //            if (!existingUser.IsEnabled &&
        //                Guid.TryParse(existingUser.Id, out var userId))
        //            {
        //                await _securityService.ToggleUserIsEnabled(userId, true);
        //            }

        //            account.AspNetUserId = existingUser.Id;
        //        }
        //        else
        //        {
        //            var result = await _userManager.CreateAsync(user, password);
        //            if (!result.Succeeded)
        //            {
        //                var error = string.Join(". ", result.Errors.Select(x => x.Description));
        //                return ServiceResult<string>.Failure(error);
        //            }

        //            account.AspNetUserId = user.Id;
        //        }

        //        bool sendEmail = false;

        //        if (existingUser == null)
        //        {
        //            _userAccountRepository.Insert(account);
        //            sendEmail = true;
        //        }
        //        else
        //        {
        //            var existingAccount =
        //        await _userAccountRepository.GetByAspNetIdActiveOrDeletedAsync(existingUser.Id);

        //            if (existingAccount == null)
        //            {
        //                _userAccountRepository.Insert(account);
        //                sendEmail = true;
        //            }
        //            else
        //            {
        //                account.Id = existingAccount.Id;
        //                _userAccountRepository.Update(account);
        //            }
        //        }

        //        _userAccountRepository.Commit();

        //        if (model.AbsUser || model.ClientUser)
        //        {
        //            await _securityService.UpdateRolesForUser(
        //                existingUser ?? user,
        //                model.UpdatedRole);
        //        }
        //        else
        //        {
        //        await _securityService.UpdateRolesForUser(user.Id, model.UpdatedRole);

        //        }

        //        if (sendEmail)
        //        {
        //            if (model.AbsUser)
        //                password = "N/A";

        //            await _emailService.SendNewUserEmailAsync(
        //                user.Email,
        //                model.Email,
        //                password,
        //                currentUser.Email);
        //        }

        //        return ServiceResult<string>.Success("User updated successfully");
        //    }
        //    catch (Exception)
        //    {
        //        return ServiceResult<string>.Failure("There was an error saving user.");
        //    }
        //}

        public async Task<ServiceResult<bool>> EditAbsUserAsync(AccountViewModel model)
        {
            try
            {
                string password = "";
                var currentUser = await _securityService.GetCurrentUserAsync();
                if (!currentUser.IsSuccess)
                    return ServiceResult<bool>.Failure("Unauthorized");

                var currentUserData = currentUser.Data;

                foreach (var role in FormValues.ABSRoles)
                {
                    if (model.UpdatedRole == role &&
                        !currentUserData.Permissions.Contains(Permission.ManageAbsUsers))
                    {
                        return ServiceResult<bool>.Failure("Not authorized to assign this role.");
                    }
                }

                var adminUser =
                await _userAccountRepository.GetByAspNetIdAsync(currentUserData.Id);

                var account = model.UserAccount;

                ApplicationUser user = null;


                var userDto = await _securityService.GetUser(Guid.Parse(model.Id));

                if (userDto != null)
                {
                    user = new ApplicationUser
                    {
                        Id = userDto.Id.ToString(),
                        Email = userDto.Email,
                        UserName = userDto.Email,
                        EmailConfirmed = true,
                        IsEnabled = userDto.IsEnabled
                    };
                }

                //if (!string.IsNullOrEmpty(model.Id))
                //    user = await _securityService.GetUser(Guid.Parse(model.Id));

                if (user == null)
                {
                    password = GenerateSecurePassword();

                    user = new ApplicationUser
                    {
                        Email = model.Email,
                        UserName = model.Email,
                        EmailConfirmed = true,
                        IsEnabled = true
                    };

                    var result = await _userManager.CreateAsync(user, password);

                    if (!result.Succeeded)
                    {
                        var error = string.Join(". ",
                        result.Errors.Select(x => x.Description));
                        return ServiceResult<bool>.Failure(error);
                    }
                }

                user.EmailConfirmed = true;
                account.IsAgreeToPrivacy = true;
                account.AspNetUserId = user.Id;

                var existingAccount =
                await _userAccountRepository.GetByAspNetIdActiveOrDeletedAsync(user.Id);

                bool sendEmail = false;

                if (existingAccount == null)
                {
                    _userAccountRepository.Insert(account);
                    sendEmail = true;
                }
                else
                {
                    account.Id = existingAccount.Id;
                    _userAccountRepository.Update(account);
                }

                _userAccountRepository.Commit();

                // Get ApplicationDto for UpdateRolesForUser
                var userDtoForRoles = await _securityService.GetUser(Guid.Parse(user.Id));
                if (userDtoForRoles != null)
                {
                    await _securityService.UpdateRolesForUser(userDtoForRoles, model.UpdatedRole);
                }

                if (sendEmail)
                {
                    await _emailService.SendNewUserEmailAsync(
                        user.Email,
                        user.Email,
                        password,
                        currentUserData.Email);
                }

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Failure("Error saving user.");
            }
        }

        public async Task<ServiceResult<bool>> DeleteAbsUserAsync(Guid userId)
        {
            try
            {
                await _userAccountRepository.DeleteAsync(userId);
                _userAccountRepository.Commit();

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Failure("Error marking user as disabled.");
            }
        }

        public async Task<ServiceResult<bool>> StoreClientAdminAsync(AccountViewModel model)
        {
            try
            {
                string password = "";

                var currentUserResult = await _securityService.GetCurrentUserAsync();
                if (!currentUserResult.IsSuccess)
                {
                    return ServiceResult<bool>.Failure("User not authenticated");
                }

                var currentUser = currentUserResult.Data;

                foreach (var role in FormValues.ABSRoles)
                {
                    if (model.UpdatedRole == role &&
                        (currentUser.Permissions == null || !currentUser.Permissions.Contains(Permission.ManageAbsUsers)))
                    {
                        return ServiceResult<bool>.Failure("There was an error saving user.");
                    }
                }

                var adminUser = await _userAccountRepository.GetByAspNetIdAsync(currentUser.Id);

                // Initialize account with required properties
                var account = model.UserAccount ?? new UserAccountModel
                {
                    AspNetUserId = string.Empty,
                    FirstName = model.UserAccount?.FirstName ?? string.Empty,
                    LastName = model.UserAccount?.LastName ?? string.Empty,
                    Company = model.Company ?? string.Empty,
                    Email = model.Email ?? string.Empty,
                    Title = model.UserAccount?.Title ?? string.Empty,
                    ClientId = model.ClientId ?? model.UserAccount?.ClientId ?? Guid.Empty
                };

                account.Email = model.Email ?? account.Email;
                account.Company = model.Company ?? account.Company;
                account.IsAgreeToPrivacy = true;

                // Handle AspNetUser - it's a string in AccountViewModel
                ApplicationDto user = null;
                Guid parsedUserId = Guid.Empty;
                if (!string.IsNullOrEmpty(model.AspNetUser) && Guid.TryParse(model.AspNetUser, out parsedUserId))
                {
                    user = await _securityService.GetUser(parsedUserId);
                }

                // lookup existing user
                var existingUser = await _userManager.FindByEmailAsync(model.Email);

                // if user exists
                if (existingUser != null)
                {
                    if (existingUser.Roles == null)
                    {
                        existingUser.Roles = (IReadOnlyList<string>)await _userManager.GetRolesAsync(existingUser);
                    }

                    if (user == null)
                    {
                        user = await _securityService.GetUser(Guid.Parse(existingUser.Id));
                    }

                    // and user is not enabled
                    if (!existingUser.IsEnabled)
                    {
                        if (Guid.TryParse(existingUser.Id, out var existingUserId))
                        {
                            // re-enable user
                            await _securityService.ToggleUserIsEnabled(existingUserId, true);
                        }
                    }

                    account.AspNetUserId = existingUser.Id;
                }
                else
                {
                    // Generate password for new user
                    password = GenerateSecurePassword();

                    // Create new ApplicationUser
                    var newUser = new ApplicationUser
                    {
                        Email = model.Email,
                        UserName = model.Email,
                        EmailConfirmed = true,
                        IsEnabled = true
                    };

                    var result = await _userManager.CreateAsync(newUser, password);

                    if (!result.Succeeded)
                    {
                        var error = string.Join(". ", result.Errors.Select(x => x.Description));
                        return ServiceResult<bool>.Failure($"Failed to create account. Details: {error}");
                    }

                    account.AspNetUserId = newUser.Id;
                    user = await _securityService.GetUser(Guid.Parse(newUser.Id));
                }

                bool sendEmail = false;

                if (existingUser == null)
                {
                    _userAccountRepository.Insert(account);
                    sendEmail = true;
                }
                else
                {
                    var existingAccount = await _userAccountRepository.GetByAspNetId_ActiveOrDeleted(existingUser.Id);

                    if (existingAccount == null)
                    {
                        _userAccountRepository.Insert(account);
                        sendEmail = true;
                    }
                    else
                    {
                        account.Id = existingAccount.Id;
                        _userAccountRepository.Update(account);
                    }
                }

                _userAccountRepository.Commit();

                // Update roles
                if (model.AbsUser || model.ClientUser)
                {
                    await _securityService.UpdateRolesForUser(user, model.UpdatedRole);
                }
                else
                {
                    await _securityService.UpdateRolesForUser(user, AppConstants.Roles.ClientAdmin);
                }

                if (sendEmail)
                {
                    if (model.AbsUser || model.ClientUser)
                    {
                        if (model.AbsUser)
                            password = "N/A";

                        await SendNewUserEmailAsync(user, account, adminUser, model, "Access Granted", password, currentUser.Email);
                    }
                    else
                    {
                        await SendNewUserEmailAsync(user, account, adminUser, model, "CAP Client Admin Created", password, currentUser.Email);
                    }
                }

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Failure($"There was an error saving user: {ex.Message}");
            }
        }

        private async Task SendNewUserEmailAsync(
            ApplicationDto user,
            UserAccountModel account,
            UserAccountModel adminUser,
            AccountViewModel model,
            string subject,
            string password,
            string ccEmail)
        {
            try
            {
                var template = await _templateReader.ReadTemplate(DigitalCap.Core.Constants.Constants.EmailTemplates.ClientCreated);
                string body = template
                    .Replace("{userEmail}", user.Email)
                    .Replace("{userName}", $"{account.FirstName} {account.LastName}")
                    .Replace("{companyName}", model.Company)
                    .Replace("{accountCreator}", $"{adminUser?.FirstName} {adminUser?.LastName}")
                    .Replace("{accountEmail}", user.Email)
                    .Replace("{newPassword}", password)
                    .Replace("{appLink}", ""); // App link can be configured via settings

                if (string.IsNullOrEmpty(ccEmail))
                {
                    await _emailService.SendEmailAsync(user.Email, subject, "", body);
                }
                else
                {
                    await _emailService.SendEmailAsyncWithCC(user.Email, ccEmail, subject, "", body);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                System.Diagnostics.Debug.WriteLine($"Failed to send confirmation email to user. Details: {ex.Message}");
            }
        }

        public async Task<ServiceResult<bool>> StoreAbsUserAsync(AccountViewModel model)
        {
            try
            {
                if (model == null)
                    return ServiceResult<bool>.Failure("Invalid request.");

                string password = string.Empty;

                var currentUserResult = await _securityService.GetCurrentUserAsync();

                if (!currentUserResult.IsSuccess || currentUserResult.Data == null)
                    return ServiceResult<bool>.Failure("Unauthorized");

                var currentUser = currentUserResult.Data;


                if (FormValues.ABSRoles.Contains(model.UpdatedRole) &&
                    !(currentUser.Permissions?.Contains(Permission.ManageAbsUsers) ?? false))
                {
                    return ServiceResult<bool>.Failure("You do not have permission to assign this role.");
                }

                var adminUser = await _userAccountRepository.GetByAspNetIdAsync(currentUser.Id);

                var account = model.UserAccount;
                account.IsAgreeToPrivacy = true;

                var existingIdentityUser = await _userManager.FindByEmailAsync(model.Email);

                bool isEdit = !model.NewUser && (!string.IsNullOrEmpty(model.AspNetUser) || !string.IsNullOrEmpty(account?.AspNetUserId));

                ApplicationDto userDto = null;

                if (existingIdentityUser != null)
                {
                    if (!isEdit || (existingIdentityUser.Id != model.AspNetUser && existingIdentityUser.Id != account?.AspNetUserId))
                    {
                        return ServiceResult<bool>.Failure("This email address already exists. Please use a different email address.");
                    }

                    if (Guid.TryParse(existingIdentityUser.Id, out var existingUserId))
                    {
                        userDto = await _securityService.GetUser(existingUserId);
                    }

                    // enable if disabled
                    if (!existingIdentityUser.IsEnabled &&
                        Guid.TryParse(existingIdentityUser.Id, out var disabledUserId))
                    {
                        await _securityService.ToggleUserIsEnabled(disabledUserId, true);
                    }

                    account.AspNetUserId = existingIdentityUser.Id;
                }
                else
                {
                    // 6️⃣ Create new identity user
                    password = GenerateSecurePassword();
                    var newUser = new ApplicationUser
                    {
                        Email = model.Email,
                        UserName = model.Email,
                        EmailConfirmed = true,
                        IsEnabled = true
                    };

                    var createResult = await _userManager.CreateAsync(newUser, password);

                    if (!createResult.Succeeded)
                    {
                        var error = string.Join(". ", createResult.Errors.Select(x => x.Description));
                        return ServiceResult<bool>.Failure($"User creation failed: {error}");
                    }

                    account.AspNetUserId = newUser.Id;

                    userDto = await _securityService.GetUser(Guid.Parse(newUser.Id));
                }

                if (userDto == null)
                    return ServiceResult<bool>.Failure("User loading failed.");

                userDto.EmailConfirmed = true;

                // 7️⃣ Insert / Update account
                bool sendEmail = false;

                if (existingIdentityUser == null)
                {
                    _userAccountRepository.Insert(account);
                    sendEmail = true;
                }
                else
                {
                    var existingAccount =
                        await _userAccountRepository.GetByAspNetId_ActiveOrDeleted(existingIdentityUser.Id);

                    if (existingAccount == null)
                    {
                        _userAccountRepository.Insert(account);
                        sendEmail = true;
                    }
                    else
                    {
                        account.Id = existingAccount.Id;
                        _userAccountRepository.Update(account);
                    }
                }

                // 8️⃣ Role assignment
                if (model.AbsUser || model.ClientUser)
                {
                    await _securityService.UpdateRolesForUser(userDto, model.UpdatedRole);
                }
                else
                {
                    await _securityService.UpdateRolesForUser(userDto, AppConstants.Roles.ClientAdmin);
                }

                // 9️⃣ Commit after role update
                _userAccountRepository.Commit();

                // 🔟 Email
                if (sendEmail)
                {
                    if (model.AbsUser)
                        password = "N/A";

                    await SendNewUserEmailAsync(
                        userDto,
                        account,
                        adminUser,
                        model,
                        model.AbsUser || model.ClientUser
                            ? "Access Granted"
                            : "CAP Client Admin Created",
                        password,
                        currentUser.Email);
                }

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Failure($"Error saving ABS user: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates a secure random password with uppercase, lowercase, numbers, and special characters.
        /// Password length: 8 characters
        /// </summary>
        /// <returns>A secure random password string</returns>
        private string GenerateSecurePassword()
        {
            // Character set includes: uppercase, lowercase, numbers, and special characters
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var bytes = new byte[8];
            
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            
            return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
        }

    }
}
