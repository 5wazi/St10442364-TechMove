using PROG7313_TechMove.ViewModels;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PROG7313_TechMove.Services
{
    public class ApiClient
    {
        private readonly HttpClient _http;
        private readonly IHttpContextAccessor _ctx;
        private readonly ILogger<ApiClient> _logger;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ApiClient(
            HttpClient http,
            IHttpContextAccessor ctx,
            ILogger<ApiClient> logger)
        {
            _http = http;
            _ctx = ctx;
            _logger = logger;
        }

        // ── Builds an HttpRequestMessage with the JWT attached per-request ────
        // Using per-request headers instead of DefaultRequestHeaders prevents
        // the shared HttpClient from losing or mixing up tokens between calls.

        private HttpRequestMessage BuildRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            var token = _ctx.HttpContext?.Request.Cookies["techmove_jwt"];

            Console.WriteLine($"[APICLIENT] {method} {url}");
            Console.WriteLine($"[APICLIENT] Cookie token: " +
                              $"{(token != null ? "present, length=" + token.Length : "NULL")}");

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                Console.WriteLine($"[APICLIENT] Authorization header set.");
            }
            else
            {
                Console.WriteLine($"[APICLIENT] WARNING: No token found in cookie.");
            }

            return request;
        }

        // ── Auth ──────────────────────────────────────────────────────────────

        public async Task<(bool ok, string? jwt, string? email)> LoginAsync(
            string firebaseIdToken)
        {
            try
            {
                
                var body = JsonContent.Create(new { idToken = firebaseIdToken });
                var resp = await _http.PostAsync("api/auth/login", body);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Login failed with status {Code}.", resp.StatusCode);
                    return (false, null, null);
                }

                var content = await resp.Content.ReadAsStringAsync();
                var obj = JsonSerializer.Deserialize<JsonElement>(content, _json);
                var jwt = obj.GetProperty("jwt").GetString();
                var email = obj.GetProperty("email").GetString();
                return (true, jwt, email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoginAsync threw an exception.");
                return (false, null, null);
            }
        }

        // ── Clients ───────────────────────────────────────────────────────────

        public async Task<List<ClientViewModel>> GetClientsAsync()
        {
            try
            {
                var req = BuildRequest(HttpMethod.Get, "api/clients");
                var resp = await _http.SendAsync(req);

                _logger.LogInformation("GetClientsAsync: {StatusCode}", resp.StatusCode);
                if (!resp.IsSuccessStatusCode) return new();

                return await Deserialize<List<ClientViewModel>>(resp) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetClientsAsync failed.");
                return new();
            }
        }

        public async Task<ClientViewModel?> GetClientAsync(int id)
        {
            try
            {
                var req = BuildRequest(HttpMethod.Get, $"api/clients/{id}");
                var resp = await _http.SendAsync(req);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
                if (!resp.IsSuccessStatusCode) return null;

                return await Deserialize<ClientViewModel>(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetClientAsync({Id}) failed.", id);
                return null;
            }
        }

        public async Task<ClientViewModel?> CreateClientAsync(CreateClientViewModel vm)
        {
            try
            {
                var req = BuildRequest(HttpMethod.Post, "api/clients");
                req.Content = JsonContent.Create(vm);

                var resp = await _http.SendAsync(req);

                _logger.LogInformation("CreateClientAsync: {StatusCode}", resp.StatusCode);
                if (!resp.IsSuccessStatusCode) return null;

                return await Deserialize<ClientViewModel>(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateClientAsync failed.");
                return null;
            }
        }

        // ── Contracts ─────────────────────────────────────────────────────────

        public async Task<List<ContractViewModel>> GetContractsAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? status = null)
        {
            try
            {
                var query = new StringBuilder("api/contracts?");
                if (fromDate.HasValue)
                    query.Append($"fromDate={fromDate.Value:yyyy-MM-dd}&");
                if (toDate.HasValue)
                    query.Append($"toDate={toDate.Value:yyyy-MM-dd}&");
                if (!string.IsNullOrEmpty(status))
                    query.Append($"status={Uri.EscapeDataString(status)}&");

                var url = query.ToString().TrimEnd('&', '?');
                var req = BuildRequest(HttpMethod.Get, url);
                var resp = await _http.SendAsync(req);

                _logger.LogInformation("GetContractsAsync: {StatusCode}", resp.StatusCode);
                if (!resp.IsSuccessStatusCode) return new();

                return await Deserialize<List<ContractViewModel>>(resp) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetContractsAsync failed.");
                return new();
            }
        }

        public async Task<ContractViewModel?> GetContractAsync(int id)
        {
            try
            {
                var req = BuildRequest(HttpMethod.Get, $"api/contracts/{id}");
                var resp = await _http.SendAsync(req);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
                if (!resp.IsSuccessStatusCode) return null;

                return await Deserialize<ContractViewModel>(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetContractAsync({Id}) failed.", id);
                return null;
            }
        }

        public async Task<(bool ok, string? error)> CreateContractAsync(
            ContractCreateViewModel vm)
        {
            try
            {
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(vm.ClientId.ToString()), "clientId");
                form.Add(new StringContent(vm.StartDate.ToString("yyyy-MM-dd")), "startDate");
                form.Add(new StringContent(vm.EndDate.ToString("yyyy-MM-dd")), "endDate");
                form.Add(new StringContent(vm.ServiceLevel), "serviceLevel");

                if (vm.SignedAgreement != null && vm.SignedAgreement.Length > 0)
                {
                    var stream = vm.SignedAgreement.OpenReadStream();
                    var content = new StreamContent(stream);
                    content.Headers.ContentType =
                        new MediaTypeHeaderValue("application/pdf");
                    form.Add(content, "signedAgreement", vm.SignedAgreement.FileName);
                }

                var req = BuildRequest(HttpMethod.Post, "api/contracts");
                req.Content = form;
                var resp = await _http.SendAsync(req);

                _logger.LogInformation("CreateContractAsync: {StatusCode}", resp.StatusCode);
                if (resp.IsSuccessStatusCode) return (true, null);

                var body = await resp.Content.ReadAsStringAsync();
                try
                {
                    var err = JsonSerializer.Deserialize<JsonElement>(body, _json);
                    return (false, err.GetProperty("message").GetString());
                }
                catch { return (false, "An unexpected error occurred."); }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateContractAsync threw an exception.");
                return (false, "Could not reach the API. Please try again.");
            }
        }

        public async Task<bool> ChangeStatusAsync(int contractId, string newStatus)
        {
            try
            {
                var req = BuildRequest(HttpMethod.Patch,
                                  $"api/contracts/{contractId}/status");
                req.Content = JsonContent.Create(new { newStatus });
                var resp = await _http.SendAsync(req);

                _logger.LogInformation("ChangeStatusAsync: {StatusCode}", resp.StatusCode);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChangeStatusAsync failed.");
                return false;
            }
        }

        public async Task<byte[]?> DownloadAgreementAsync(int contractId)
        {
            try
            {
                var req = BuildRequest(HttpMethod.Get,
                               $"api/contracts/{contractId}/download");
                var resp = await _http.SendAsync(req);

                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DownloadAgreementAsync({Id}) failed.", contractId);
                return null;
            }
        }

        // ── Service Requests ──────────────────────────────────────────────────

        public async Task<List<ServiceRequestViewModel>> GetServiceRequestsByContractAsync(
            int contractId)
        {
            try
            {
                var req = BuildRequest(HttpMethod.Get,
                               $"api/servicerequests/by-contract/{contractId}");
                var resp = await _http.SendAsync(req);

                if (!resp.IsSuccessStatusCode) return new();
                return await Deserialize<List<ServiceRequestViewModel>>(resp) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetServiceRequestsByContractAsync({Id}) failed.", contractId);
                return new();
            }
        }

        public async Task<ServiceRequestViewModel?> GetServiceRequestAsync(int id)
        {
            try
            {
                var req = BuildRequest(HttpMethod.Get, $"api/servicerequests/{id}");
                var resp = await _http.SendAsync(req);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
                if (!resp.IsSuccessStatusCode) return null;

                return await Deserialize<ServiceRequestViewModel>(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetServiceRequestAsync({Id}) failed.", id);
                return null;
            }
        }

        public async Task<(bool ok, string? error)> CreateServiceRequestAsync(
            ServiceRequestCreateViewModel vm)
        {
            try
            {
                var req = BuildRequest(HttpMethod.Post, "api/servicerequests");
                req.Content = JsonContent.Create(vm);
                var resp = await _http.SendAsync(req);

                _logger.LogInformation("CreateServiceRequestAsync: {StatusCode}", resp.StatusCode);
                if (resp.IsSuccessStatusCode) return (true, null);

                var body = await resp.Content.ReadAsStringAsync();
                try
                {
                    var err = JsonSerializer.Deserialize<JsonElement>(body, _json);
                    return (false, err.GetProperty("message").GetString());
                }
                catch { return (false, "An unexpected error occurred."); }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateServiceRequestAsync threw an exception.");
                return (false, "Could not reach the API. Please try again.");
            }
        }

        public async Task<decimal> GetExchangeRateAsync()
        {
            try
            {
                var req = BuildRequest(HttpMethod.Get, "api/servicerequests/rate");
                var resp = await _http.SendAsync(req);

                if (!resp.IsSuccessStatusCode) return 18.50m;

                var obj = await Deserialize<JsonElement?>(resp);
                if (obj.HasValue &&
                    obj.Value.TryGetProperty("rate", out var rateProp))
                    return rateProp.GetDecimal();

                return 18.50m;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetExchangeRateAsync failed. Using fallback.");
                return 18.50m;
            }
        }

        // ── Private deserialise helper ────────────────────────────────────────

        private static async Task<T?> Deserialize<T>(HttpResponseMessage resp)
        {
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, _json);
        }
    }
}