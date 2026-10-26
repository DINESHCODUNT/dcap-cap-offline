using DigitalCap.Core.Constants;
using DigitalCap.Core.DTO;
using DigitalCap.Core.Helpers.Constants;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.View.Admin;
using DigitalCap.Core.Security;
using DigitalCap.Core.ViewModels;
using DigitalCap.WebApi.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.Text;
using static DigitalCap.Core.DTO.ClientProfileDTO;

namespace DigitalCap.Infrastructure.Service
{
    public class ClientProfileService : IClientProfileService                                                                                                                                          
    {
        private readonly IClientProfileRepository _clientProfileRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ISecurityService _securityService;
        private readonly IUserAccountRepository _userAccountRepository;
        private readonly IEmailService _emailService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ClientProfileService> _logger;
        private readonly ITemplateReader _templateReader;
        private readonly RoleManager<IdentityRole> _roleManager;


        public ClientProfileService(IClientProfileRepository clientProfileRepository,
            IProjectRepository projectRepository, ISecurityService securityService,
            IUserAccountRepository userAccountRepository, IEmailService emailService,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            ILogger<ClientProfileService> logger, ITemplateReader templateReader)
        {
            _clientProfileRepository = clientProfileRepository;
            _projectRepository = projectRepository;

            _securityService = securityService;
            _userAccountRepository = userAccountRepository;
            _emailService = emailService;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _templateReader = templateReader;
        }

        public async Task<ServiceResult<ClientProfileViewModel>> GetClientProfileByIdAsync(Guid clientId)
        {
            try
            {
                if (clientId == Guid.Empty)
                    return ServiceResult<ClientProfileViewModel>.Failure("Invalid client ID");

                var client = await _clientProfileRepository.GetClientAsync(clientId);

                if (client == null)
                    return ServiceResult<ClientProfileViewModel>.Failure("Client not found");

                var projectCounts = await _projectRepository.GetProjectCountGroupedByClientIdAsync();
                var projectCountMap = projectCounts.ToDictionary(x => x.ClientId, x => (uint)x.ProjectCount);
                var projectCount = projectCountMap.TryGetValue(clientId, out var count) ? (int)count : 0;

                var viewModel = new ClientProfileViewModel
                {
                    Id = client.Id,
                    Wcn = client.Wcn,
                    CompanyName = client.CompanyName,
                    CompanyAddress = client.CompanyAddress,
                    CompanyBillingEmail = client.CompanyBillingEmail,
                    CompanyBillingSystemUrl = client.CompanyBillingSystemUrl,
                    ProjectCount = projectCount
                };
                return ServiceResult<ClientProfileViewModel>.Success(viewModel);
            }
            catch (Exception ex)
            {
                return ServiceResult<ClientProfileViewModel>.Failure($"Error retrieving client profile: {ex.Message}");
            }
        }


        public async Task<ServiceResult<List<ClientSummaryDto>>> GetClientSummariesAsync()
        {
            var clients = await _clientProfileRepository.GetAllClientsAsync();

            var projectCounts = await _projectRepository.GetProjectCountGroupedByClientIdAsync();

            var projectCountMap = projectCounts
            .ToDictionary(x => x.ClientId, x => (uint)x.ProjectCount);

            var result = clients.Select(client => new ClientSummaryDto
            {
                Id = client.Id,
                CompanyName = client.CompanyName,
                Wcn = uint.TryParse(client.Wcn, out var wcn) ? wcn.ToString() : null,
                CompanyAddress = client.CompanyAddress,
                CompanyBillingEmail = client.CompanyBillingEmail,
                CompanyBillingSystemUrl = client.CompanyBillingSystemUrl,
                ProjectCount = (int)(projectCountMap.TryGetValue(client.Id, out var count)
                    ? count
                    : 0)
            }).ToList();

            return ServiceResult<List<ClientSummaryDto>>.Success(result);
        }


        public async Task<ServiceResult<ClientProfileViewModel>> CreateClientProfileAsync(ClientProfileViewModel model)
        {
            var client = new Client
            {
                Id = Guid.NewGuid(),
                CompanyName = model.CompanyName,
                CompanyAddress = model.CompanyAddress ?? string.Empty,
                CompanyBillingEmail = model.CompanyBillingEmail ?? string.Empty,
                CompanyBillingSystemUrl = model.CompanyBillingSystemUrl ?? string.Empty,
                Wcn = model.Wcn?.ToString()
            };

            await _clientProfileRepository.AddAsync(client);

            var projectCounts = await _projectRepository.GetProjectCountGroupedByClientIdAsync();
            var projectCountMap = projectCounts.ToDictionary(x => x.ClientId, x => (uint)x.ProjectCount);

            var projectCount = projectCountMap.TryGetValue(client.Id, out var count) ? (int)count : 0;

            var result = new ClientProfileViewModel
            {
                Id = client.Id,
                CompanyName = client.CompanyName,
                CompanyAddress = client.CompanyAddress,
                CompanyBillingEmail = client.CompanyBillingEmail,
                CompanyBillingSystemUrl = client.CompanyBillingSystemUrl,
                Wcn = client.Wcn,
                ProjectCount = projectCount
            };

            return ServiceResult<ClientProfileViewModel>.Success(result);
        }


        public async Task<ServiceResult<Guid>> EditClientProfileAsync(ClientProfileViewModel model)
        {
            try
            {
                if (model == null)
                    return ServiceResult<Guid>.Failure("Invalid request");


                if (model.Id == Guid.Empty)
                    return ServiceResult<Guid>.Failure("Invalid client ID for edit operation");

                var clients = await _clientProfileRepository.GetAllClientsAsync();

                bool duplicateExists = !string.IsNullOrWhiteSpace(model.Wcn) && clients.Any(c => c.Wcn == model.Wcn && c.Id != model.Id);
                if (duplicateExists)
                    return ServiceResult<Guid>.Failure("DuplicateWCN");

                var client = await _clientProfileRepository.GetClientAsync(model.Id);

                if (client == null)
                    return ServiceResult<Guid>.Failure("Client not found");

                var originalId = client.Id;

                client.CompanyName = model.CompanyName;
                client.CompanyAddress = model.CompanyAddress ?? string.Empty;
                client.CompanyBillingEmail = model.CompanyBillingEmail ?? string.Empty;
                client.CompanyBillingSystemUrl = model.CompanyBillingSystemUrl ?? string.Empty;
                client.Wcn = model.Wcn;

                if (client.Id == Guid.Empty)
                {

                    client.Id = originalId;
                }
                var savedId = await _clientProfileRepository.SaveClientProfileAsync(client);

                return ServiceResult<Guid>.Success(savedId);
            }
            catch (Exception ex)
            {
                return ServiceResult<Guid>.Failure($"Error saving client profile: {ex.Message}");
            }
        }

        //for Project history
        public async Task<ServiceResult<ClientProfileDetailDto>> GetClientProfileWithDetailsAsync(Guid clientId, bool freshmeat = false)
        {
            try
            {
                if (clientId == Guid.Empty)
                    return ServiceResult<ClientProfileDetailDto>.Failure("Invalid client ID");

                var client = await _clientProfileRepository.GetClientAsync(clientId);

                if (client == null)
                    return ServiceResult<ClientProfileDetailDto>.Failure("Client not found");

                List<ProjectSummaryDto> projects;
                //List<ClientAdminDto> admins;

                if (freshmeat)
                {
                    projects = new List<ProjectSummaryDto>();
                   // admins = new List<ClientAdminDto>();
                }
                else
                {

                    var projectsData = await _projectRepository.GetProjectsByClientIdAsync(clientId);

                    projects = projectsData.Select(p => new ProjectSummaryDto(p)).ToList();

                    //  Get client admins
                    //admins = await _userAccountRepository.GetClientAdminsAsync(clientId);

                    
                }

                var adminsData = await _clientProfileRepository.GetClientAdminsAsync(clientId);
var admins = adminsData.ToList();

                var result = new ClientProfileDetailDto
                {
                    Id = client.Id,
                    Wcn = client.Wcn,
                    CompanyName = client.CompanyName,
                    CompanyAddress = client.CompanyAddress,
                    CompanyBillingEmail = client.CompanyBillingEmail,
                    CompanyBillingSystemUrl = client.CompanyBillingSystemUrl,
                    Projects = projects,
                    ClientAdmins = admins,
                    Freshmeat = freshmeat
                };

                _logger.LogInformation($"Retrieved client profile for {client.CompanyName} with {projects.Count} projects and {admins.Count} admins");

                return ServiceResult<ClientProfileDetailDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetClientProfileWithDetailsAsync for clientId: {clientId}");
                return ServiceResult<ClientProfileDetailDto>.Failure($"Error retrieving client profile: {ex.Message}");
            }
        }

        public async Task<ServiceResult<ClientProfileViewModel>> AddClientProfileAsync(ClientProfileViewModel model)
        {
            if (model == null)
                return ServiceResult<ClientProfileViewModel>.Failure("Model cannot be null");

            var client = new Client
            {
                Id = Guid.NewGuid(),
                CompanyName = model.CompanyName,
                CompanyAddress = model.CompanyAddress,
                CompanyBillingEmail = model.CompanyBillingEmail,
                CompanyBillingSystemUrl = model.CompanyBillingSystemUrl,
                Wcn = model.Wcn?.ToString()
            };

            await _clientProfileRepository.AddAsync(client);

            var result = new ClientProfileViewModel
            {
                Id = client.Id,
                CompanyName = client.CompanyName,
                Wcn = model.Wcn?.ToString()
            };

            return ServiceResult<ClientProfileViewModel>.Success(result);
        }

        public Task<ServiceResult<AddClientAdminInitDto>> GetAddClientAdminInitAsync(Guid clientId, string company)
        {
            var result = new AddClientAdminInitDto
            {
                ClientId = clientId,
                Company = company
            };

            return Task.FromResult(
                ServiceResult<AddClientAdminInitDto>.Success(result));
        }

        //public async Task<ServiceResult<bool>> StoreClientAdminAsync(AccountViewModel model)
        //{
        //    try
        //    {
        //        string password = "";
        //        var currentUserResult = await _securityService.GetCurrentUserAsync();
        //        if (!currentUserResult.IsSuccess)
        //        {
        //            return ServiceResult<bool>.Failure("User not authenticated");
        //        }

        //        var currentUser = currentUserResult.Data;

        //        // Convert ApplicationDto to ApplicationUser-like object for permission check
        //        // Note: In Web API, we use ApplicationDto instead of ApplicationUser
        //        foreach (var role in FormValues.ABSRoles)
        //        {
        //            if (model.UpdatedRole == role &&
        //                (currentUser.Permissions == null || !currentUser.Permissions.Contains(Permission.ManageAbsUsers)))
        //            {
        //                return ServiceResult<bool>.Failure("There was an error saving user.");
        //            }
        //        }

        //        var adminUser = await _userAccountRepository.GetByAspNetIdAsync(currentUser.Id);

        //        // Initialize account with required properties
        //        var account = model.UserAccount ?? new UserAccountModel
        //        {
        //            AspNetUserId = string.Empty, // Will be set later
        //            FirstName = model.UserAccount?.FirstName ?? string.Empty,
        //            LastName = model.UserAccount?.LastName ?? string.Empty,
        //            Company = model.Company ?? string.Empty,
        //            Email = model.Email ?? string.Empty,
        //            Title = model.UserAccount?.Title ?? string.Empty,
        //            ClientId = model.ClientId ?? model.UserAccount?.ClientId ?? Guid.Empty
        //        };

        //        // Set account properties
        //        account.Email = model.Email ?? account.Email;
        //        account.Company = model.Company ?? account.Company;
        //        account.IsAgreeToPrivacy = true;
        //        if (model.UserAccount != null)
        //        {
        //            account.FirstName = model.UserAccount.FirstName ?? account.FirstName;
        //            account.LastName = model.UserAccount.LastName ?? account.LastName;
        //            account.Phone = model.UserAccount.Phone;
        //            account.TimeZone = model.UserAccount.TimeZone ?? account.TimeZone;
        //            // Handle Guid properly - can't use ?? operator on non-nullable Guid
        //            if (model.ClientId.HasValue && model.ClientId.Value != Guid.Empty)
        //                account.ClientId = model.ClientId.Value;
        //            else if (model.UserAccount.ClientId != Guid.Empty)
        //                account.ClientId = model.UserAccount.ClientId;
        //            account.Title = model.UserAccount.Title ?? account.Title;
        //        }

        //        // Handle AspNetUser - it's a string in AccountViewModel, parse as Guid if possible
        //        ApplicationDto user = null;
        //        Guid parsedUserId = Guid.Empty;
        //        if (!string.IsNullOrEmpty(model.AspNetUser) && Guid.TryParse(model.AspNetUser, out parsedUserId))
        //        {
        //            user = await _securityService.GetUser(parsedUserId);
        //        }

        //        // lookup existing user
        //        var existingUser = await _userManager.FindByEmailAsync(model.Email);

        //        // if user exists
        //        if (existingUser != null)
        //        {
        //            if (existingUser.Roles == null)
        //            {
        //                existingUser.Roles = (IReadOnlyList<string>)await _userManager.GetRolesAsync(existingUser);
        //            }

        //            if (user == null)
        //            {
        //                user = await _securityService.GetUser(Guid.Parse(existingUser.Id));
        //            }

        //            // and user is not enabled
        //            if (!existingUser.IsEnabled)
        //            {
        //                if (Guid.TryParse(existingUser.Id, out var existingUserId))
        //                {
        //                    // re-enable user
        //                    await _securityService.ToggleUserIsEnabled(existingUserId, true);
        //                }
        //            }

        //            account.AspNetUserId = existingUser.Id;
        //        }
        //        else
        //        {
        //            // Generate password for new user
        //            password = Guid.NewGuid().ToString("N")[^8..]; // Last 8 chars

        //            // Create new ApplicationUser
        //            var newUser = new ApplicationUser
        //            {
        //                Email = model.Email,
        //                UserName = model.Email,
        //                EmailConfirmed = true,
        //                IsEnabled = true
        //            };

        //            var result = await _userManager.CreateAsync(newUser, password);

        //            if (!result.Succeeded)
        //            {
        //                var error = string.Join(". ", result.Errors.Select(x => x.Description));
        //                return ServiceResult<bool>.Failure($"Failed to create account. Details: {error}");
        //            }

        //            account.AspNetUserId = newUser.Id;
        //            user = await _securityService.GetUser(Guid.Parse(newUser.Id));
        //        }

        //        bool sendEmail = false;

        //        if (existingUser == null)
        //        {
        //            _userAccountRepository.Insert(account);
        //            sendEmail = true;
        //        }
        //        else
        //        {
        //            var existingAccount = await _userAccountRepository.GetByAspNetId_ActiveOrDeleted(existingUser.Id);

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

        //        // Update roles
        //        if (model.AbsUser || model.ClientUser)
        //        {
        //            await _securityService.UpdateRolesForUser(user, model.UpdatedRole);
        //        }
        //        else
        //        {
        //            await _securityService.UpdateRolesForUser(user, AppConstants.Roles.ClientAdmin);
        //        }

        //        if (sendEmail)
        //        {
        //            if (model.AbsUser || model.ClientUser)
        //            {
        //                // password is not applicable for ABS users
        //                if (model.AbsUser)
        //                    password = "N/A";

        //                await SendNewUserEmailAsync(user, account, adminUser, model, "Access Granted", password, currentUser.Email);
        //            }
        //            else
        //            {
        //                await SendNewUserEmailAsync(user, account, adminUser, model, "CAP Client Admin Created", password, currentUser.Email);
        //            }
        //        }

        //        // Return success result
        //        return ServiceResult<bool>.Success(true);
        //    }
        //    catch (Exception ex)
        //    {
        //        return ServiceResult<bool>.Failure(ex.Message);
        //    }
        //}


        public async Task<ServiceResult<bool>> StoreClientAdminAsync(CreateClientAdminRequest request)
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

                var adminUser = await _userAccountRepository.GetByAspNetIdAsync(currentUser.Id);

                // Create account model
                var account = new UserAccountModel
                {
                    AspNetUserId = string.Empty,
                    FirstName = request.FirstName ?? "",
                    LastName = request.LastName ?? "",
                    Company = request.Company ?? "",
                    Email = request.Email ?? "",
                    Phone = request.Phone ?? "",
                    TimeZone = request.TimeZone ?? "",
                    Title = "Client Admin",
                    ClientId = request.ClientId,
                    IsAgreeToPrivacy = true
                };

                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(request.Email);

                ApplicationDto user = null;

                if (existingUser != null)
                {
                    if (existingUser.Roles == null)
                    {
                        existingUser.Roles = (IReadOnlyList<string>)await _userManager.GetRolesAsync(existingUser);
                    }

                    user = await _securityService.GetUser(Guid.Parse(existingUser.Id));

                    if (!existingUser.IsEnabled)
                    {
                        await _securityService.ToggleUserIsEnabled(Guid.Parse(existingUser.Id), true);
                    }

                    account.AspNetUserId = existingUser.Id;
                }
                else
                {
                    // Generate random password
                    password = Guid.NewGuid().ToString("N")[^8..];

                    var newUser = new ApplicationUser
                    {
                        Email = request.Email,
                        UserName = request.Email,
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

                // Assign role
                await _securityService.UpdateRolesForUser(user, request.Role ?? AppConstants.Roles.ClientAdmin);

                if (sendEmail)
                {
                    await SendNewUserEmailAsync(
                        user,
                        account,
                        adminUser,
                        null,
                        "CAP Client Admin Created",
                        password,
                        currentUser.Email
                    );
                }

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Failure(ex.Message);
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
                // You might want to add logging here
                System.Diagnostics.Debug.WriteLine($"Failed to send confirmation email to user. Details: {ex.Message}");
            }
        }



        public async Task<ClientSummaryDto> GetClientProfileAsync(Guid clientId)
        {
            var client = await _clientProfileRepository.GetClientAsync(clientId);

            if (client == null)
                throw new KeyNotFoundException("Client not found");

            return new ClientSummaryDto
            {
                Id = client.Id,
                Wcn = client.Wcn,
                CompanyName = client.CompanyName
            };
        }

        public async Task<ServiceResult<Guid>> SaveClientProfileAsync(ClientProfileViewModel model, bool freshmeat)
        {
            try
            {
                if (model == null)
                    return ServiceResult<Guid>.Failure("Invalid request");

                var clientList = await _clientProfileRepository.GetAllClients();

                if (model.Id == Guid.Empty &&
                    !string.IsNullOrWhiteSpace(model.Wcn) &&
                    clientList.Any(c => c.Wcn == model.Wcn.ToString()))
                {
                    return ServiceResult<Guid>.Failure("DuplicateWCN");
                }
                var client = new Client
                {
                    Id = model.Id,
                    CompanyName = model.CompanyName,
                    Wcn = model.Wcn?.ToString(),
                    // map remaining properties
                };

                var clientId = await _clientProfileRepository.SaveClientProfileAsync(client);


                return ServiceResult<Guid>.Success(clientId);
            }
            catch (Exception)
            {
                return ServiceResult<Guid>.Failure("Error storing client profile");
            }
        }



        public async Task<ServiceResult<bool>> DeleteClientProfileAsync(Guid Id)
        {
            try
            {
                var client = await _clientProfileRepository.GetClientAsync(Id);

                if (client == null)
                    return ServiceResult<bool>.Failure("Client not found");

                await _clientProfileRepository.DeleteClientAsync(Id);

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Failure($"Error deleting client profile: {ex.Message}");  // Return here
            }

        }



        public async Task<ServiceResult<bool>> DeleteClientAdminAsync(string aspNetUserId)
        {
            var userAccount =
            await _userAccountRepository.GetByAspNetId(aspNetUserId);

            if (userAccount == null)
                throw new KeyNotFoundException("User not found.");

            await _userAccountRepository.DeleteAsync(userAccount.Id);
            _userAccountRepository.Commit();
            return ServiceResult<bool>.Success(true);
        }

        public async Task<RetrieveCompanyNameResponseDto> RetrieveCompanyNameAsync(uint wcn)
        {
            // Check if client already exists with this WCN
            var allClients = await _clientProfileRepository.GetAllClients();
            var existingClient = allClients.FirstOrDefault(c => c.Wcn == wcn.ToString());

            if (existingClient != null)
            {
                return new RetrieveCompanyNameResponseDto
                {
                    CompanyName = existingClient.CompanyName,
                    Exists = true
                };
            }

            // TODO: Implement actual WCN lookup from external source
            // For now, returning null to indicate not found
            // This should be replaced with actual implementation that calls the WCN service
            return new RetrieveCompanyNameResponseDto
            {
                CompanyName = null,
                Exists = false
            };
        }


        public async Task<ServiceResult<AddClientAdminResponse>> GetAddClientAdminData(Guid clientId, string companyName)
        {
            try
            {
                var result = new AddClientAdminResponse
                {
                    ClientId = clientId,
                    Company = companyName,
                    DefaultRole = "ClientAdmin"
                };

                return ServiceResult<AddClientAdminResponse>.SuccessResult(result);
            }
            catch (Exception ex)
            {
                return ServiceResult<AddClientAdminResponse>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<bool>> AddClientAdminAsync(CreateClientAdminRequest request)
        {
            try
            {
                if (request == null)
                    return ServiceResult<bool>.Failure("Invalid request.");

                // STEP 1: Check if user already exists in AspNetUsers
                var existingUser = await _userManager.FindByEmailAsync(request.Email);

                ApplicationUser identityUser;

                if (existingUser == null)
                {
                    // Generate password
                    var password = GenerateTemporaryPassword();

                    identityUser = new ApplicationUser
                    {
                        Email = request.Email,
                        UserName = request.Email,
                        EmailConfirmed = true,
                        IsEnabled = true
                    };

                    var createResult = await _userManager.CreateAsync(identityUser, password);

                    if (!createResult.Succeeded)
                    {
                        var error = string.Join(", ", createResult.Errors.Select(e => e.Description));
                        return ServiceResult<bool>.Failure($"Failed to create identity user: {error}");
                    }

                    _logger.LogInformation($"Identity user created for {request.Email}");
                }
                else
                {
                    identityUser = existingUser;

                    if (!existingUser.IsEnabled)
                    {
                        await _securityService.ToggleUserIsEnabled(Guid.Parse(existingUser.Id), true);
                    }
                }

                // STEP 2: Insert into UserAccounts table
                if (request.ClientId == Guid.Empty)
                {
                    return ServiceResult<bool>.Failure("ClientId is required.");
                }
                var userAccount = new UserAccountModel
                {
                    AspNetUserId = identityUser.Id,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Phone = request.Phone,
                    TimeZone = request.TimeZone,
                    ClientId = request.ClientId,
                    Company = request.Company,
                    IsAgreeToPrivacy = true
                };

                var existingAccount =
                    await _userAccountRepository.GetByAspNetId_ActiveOrDeleted(identityUser.Id);

                if (existingAccount == null)
                {
                    _userAccountRepository.Insert(userAccount);
                }
                else
                {
                    userAccount.Id = existingAccount.Id;
                    _userAccountRepository.Update(userAccount);
                }

                _userAccountRepository.Commit();

                // Assign Role
                var userDto = await _securityService.GetUser(Guid.Parse(identityUser.Id));

                var roleName = request.Role ?? AppConstants.Roles.ClientAdmin;

                // Normalize role name
                roleName = roleName.Replace(" ", "");

                var roleExists = await _roleManager.RoleExistsAsync(roleName);

                if (!roleExists)
                {
                    var createRole = await _roleManager.CreateAsync(
                                     new IdentityRole(roleName));

                    if (!createRole.Succeeded)
                    {
                        var error = string.Join(", ", createRole.Errors.Select(e => e.Description));
                        return ServiceResult<bool>.Failure($"Failed to create role: {error}");
                    }
                }

                var isInRole = await _userManager.IsInRoleAsync(identityUser, roleName);

                if (!isInRole)
                {
                    var roleResult = await _userManager.AddToRoleAsync(identityUser, roleName);

                    if (!roleResult.Succeeded)
                    {
                        var error = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                        return ServiceResult<bool>.Failure($"Failed to assign role: {error}");
                    }
                }

                //await _securityService.UpdateRolesForUser(
                //    userDto,
                //    request.Role ?? AppConstants.Roles.ClientAdmin);

                _logger.LogInformation(
                    $"Client admin created for client {request.ClientId} with email {request.Email}");

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating client admin");
                return ServiceResult<bool>.Failure(ex.Message);
            }
        }

        private string GenerateTemporaryPassword()
        {
            const string lower = "abcdefghijklmnopqrstuvwxyz";
            const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string numbers = "0123456789";
            const string special = "!@#$%^&*";

            var random = new Random();

            return new string(new[]
            {
        upper[random.Next(upper.Length)],
        lower[random.Next(lower.Length)],
        numbers[random.Next(numbers.Length)],
        special[random.Next(special.Length)],
        upper[random.Next(upper.Length)],
        lower[random.Next(lower.Length)],
        numbers[random.Next(numbers.Length)],
        special[random.Next(special.Length)]
    });
        }
        //public async Task<ServiceResult<bool>> AddClientAdminAsync(CreateClientAdminRequest request)
        //{
        //    try
        //    {
        //        var created = await _clientProfileRepository.CreateClientAdminAsync(request);

        //        if (!created)
        //        {
        //            return ServiceResult<bool>.Failure("Failed to create client admin.");
        //        }

        //        return ServiceResult<bool>.SuccessResult(true);
        //    }
        //    catch (Exception ex)
        //    {
        //        return ServiceResult<bool>.Failure(ex.Message);
        //    }
        //}

        //public async Task<AddClientAdminResponse> GetAddClientAdminData(Guid clientId, string companyName)
        //{
        //    return new AddClientAdminResponse
        //    {
        //        ClientId = clientId,
        //        Company = companyName,
        //        DefaultRole = "ClientAdmin"
        //    };
        //}

        //public async Task<bool> AddClientAdminAsync(CreateClientAdminRequest request)
        //{
        //    return await _clientProfileRepository.CreateClientAdminAsync(request);
        //}

    }
}
