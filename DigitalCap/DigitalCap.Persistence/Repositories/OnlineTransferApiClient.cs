using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.DataTransfer;
using DigitalCap.Core.Models.Survey;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalCap.Persistence.Repositories
{
    /// <summary>
    /// HTTP client implementation of IOnlineTransferRepository that reaches online data through the
    /// website's api/DataTransferDevAPI over HTTPS instead of opening direct Azure SQL connections.
    /// </summary>
    public class OnlineTransferApiClient : IOnlineTransferRepository
    {
        private static readonly HttpClient SharedClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly string _baseUrl;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<OnlineTransferApiClient> _logger;

        public OnlineTransferApiClient(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<OnlineTransferApiClient> logger)
        {
            var url = configuration["URL"];
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException("Online server address (URL) is not configured");
            }
            _baseUrl = url.EndsWith('/') ? url : url + "/";
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
        {
            var uri = new Uri(new Uri(_baseUrl), relativePath);
            var request = new HttpRequestMessage(method, uri);

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) &&
                    !string.IsNullOrWhiteSpace(authHeader))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
                }

                if (httpContext.Request.Headers.TryGetValue("EmailId", out var emailHeader) &&
                    !string.IsNullOrWhiteSpace(emailHeader))
                {
                    request.Headers.TryAddWithoutValidation("EmailId", emailHeader.ToString());
                }
            }

            return request;
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await SharedClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var body = string.Empty;
                    try
                    {
                        body = await response.Content.ReadAsStringAsync(cancellationToken);
                    }
                    catch
                    {
                    }

                    if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        throw new HttpRequestException($"Could not authenticate with the CAP server ({response.StatusCode}). Please verify your sign-in.");
                    }

                    if ((int)response.StatusCode >= 500)
                    {
                        var snippet = body.Length > 200 ? body.Substring(0, 200) + "..." : body;
                        throw new HttpRequestException($"The CAP server returned an error ({response.StatusCode}): {snippet}");
                    }

                    throw new HttpRequestException($"Request to {request.RequestUri?.AbsolutePath} failed with status {response.StatusCode}.");
                }

                return response;
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reach CAP server at {Uri}", request.RequestUri);
                throw new HttpRequestException("Could not reach the CAP server. Ensure you have an active network connection: " + ex.Message, ex);
            }
        }

        public async Task<IsSynchedOnline?> GetDownloadedProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(HttpMethod.Get, $"api/DataTransferDevAPI/GetDownloadedProject?projectId={projectId}");
            using var response = await SendAsync(request, cancellationToken);

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            if (stream.Length == 0)
                return null;

            try
            {
                return await JsonSerializer.DeserializeAsync<IsSynchedOnline>(stream, JsonOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize IsSynchedOnline for project {ProjectId}", projectId);
                return null;
            }
        }

        public async Task AddDownloadedProjectAsync(int projectId, string userId, string userName, CancellationToken cancellationToken = default)
        {
            var path = $"api/DataTransferDevAPI/InsertDownloadedProject?projectId={projectId}&id={Uri.EscapeDataString(userId ?? string.Empty)}&username={Uri.EscapeDataString(userName ?? string.Empty)}";
            using var request = CreateRequest(HttpMethod.Post, path);
            request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

            using var response = await SendAsync(request, cancellationToken);
        }

        public async Task<global::TransferDataModel?> GetProjectDataAsync(int projectId, CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(HttpMethod.Get, $"api/DataTransferDevAPI/GetProjectData?projectId={projectId}");
            using var response = await SendAsync(request, cancellationToken);

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            if (stream.Length == 0)
                return null;

            return await JsonSerializer.DeserializeAsync<global::TransferDataModel>(stream, JsonOptions, cancellationToken);
        }

        public async Task<List<DigitalCap.Core.Models.SingleTask>> GetProjectTasksAsync(int projectId, CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(HttpMethod.Get, $"api/DataTransferDevAPI/GetProjectTasks?projectId={projectId}");
            using var response = await SendAsync(request, cancellationToken);

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            if (stream.Length == 0)
                return new List<DigitalCap.Core.Models.SingleTask>();

            var list = await JsonSerializer.DeserializeAsync<List<DigitalCap.Core.Models.SingleTask>>(stream, JsonOptions, cancellationToken);
            return list ?? new List<DigitalCap.Core.Models.SingleTask>();
        }

        public async Task<DigitalCap.Core.Models.DataTransfer.OfflineUserSecurity> GetUserSecurityAsync(string userName, CancellationToken cancellationToken = default)
        {
            var path = $"api/DataTransferDevAPI/GetUserSecurity?username={Uri.EscapeDataString(userName ?? string.Empty)}";
            using var request = CreateRequest(HttpMethod.Get, path);
            using var response = await SendAsync(request, cancellationToken);

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var sec = await JsonSerializer.DeserializeAsync<DigitalCap.Core.Models.DataTransfer.OfflineUserSecurity>(stream, JsonOptions, cancellationToken);
            return sec ?? new DigitalCap.Core.Models.DataTransfer.OfflineUserSecurity();
        }

        public async Task<List<AllSections>> GetAllSectionsAsync(int projectId, CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(HttpMethod.Get, $"api/DataTransferDevAPI/GetSectionsById?projectId={projectId}");
            using var response = await SendAsync(request, cancellationToken);

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var list = await JsonSerializer.DeserializeAsync<List<AllSections>>(stream, JsonOptions, cancellationToken);
            return list ?? new List<AllSections>();
        }

        public async Task<global::TransferDataModel> GetSectionDataAsync(int projectId, int templateId, Guid sectionId, CancellationToken cancellationToken = default)
        {
            var path = $"api/DataTransferDevAPI/GetDataByTemplateId?projectId={projectId}&templateId={templateId}&sectionId={sectionId}";
            using var request = CreateRequest(HttpMethod.Get, path);
            using var response = await SendAsync(request, cancellationToken);

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var model = await JsonSerializer.DeserializeAsync<global::TransferDataModel>(stream, JsonOptions, cancellationToken);
            return model ?? new global::TransferDataModel();
        }

        public async Task InsertSyncDataAsync(global::TransferDataModel model, CancellationToken cancellationToken = default)
        {
            using var request = CreateRequest(HttpMethod.Post, "api/DataTransferDevAPI/InsertSyncData");
            request.Content = JsonContent.Create(model, options: JsonOptions);

            using var response = await SendAsync(request, cancellationToken);
        }

        public async Task MoveToOnlineTableAsync(int projectId, int keep, CancellationToken cancellationToken = default)
        {
            var path = $"api/DataTransferDevAPI/MoveToOnlineTable?ProjectId={projectId}&Keep={keep}";
            using var request = CreateRequest(HttpMethod.Post, path);
            request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

            using var response = await SendAsync(request, cancellationToken);
        }

        public async Task ReleaseSyncDataAsync(int projectId, CancellationToken cancellationToken = default)
        {
            var path = $"api/DataTransferDevAPI/ReleaseSyncData?ProjectId={projectId}";
            using var request = CreateRequest(HttpMethod.Post, path);
            request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

            using var response = await SendAsync(request, cancellationToken);
        }

        public async Task ReleaseErrorSyncDataAsync(int projectId, CancellationToken cancellationToken = default)
        {
            var path = $"api/DataTransferDevAPI/ReleaseErrorSyncData?ProjectId={projectId}";
            using var request = CreateRequest(HttpMethod.Post, path);
            request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

            using var response = await SendAsync(request, cancellationToken);
        }

        public async Task<List<DropdownList>> GetAllDropDownProjectsAsync(string? userId, string? userName, CancellationToken cancellationToken = default)
        {
            var path = $"api/DataTransferDevAPI/GetAllDropDownProjects?id={Uri.EscapeDataString(userId ?? string.Empty)}&username={Uri.EscapeDataString(userName ?? string.Empty)}";
            using var request = CreateRequest(HttpMethod.Get, path);
            using var response = await SendAsync(request, cancellationToken);

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var list = await JsonSerializer.DeserializeAsync<List<DropdownList>>(stream, JsonOptions, cancellationToken);
            return list ?? new List<DropdownList>();
        }
    }
}
