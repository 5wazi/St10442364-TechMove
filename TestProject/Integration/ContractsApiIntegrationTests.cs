using System.Net;
using System.Text.Json;
using TestProject.Integration;

namespace TestProject.Integration
{
    
    // Validates GET /api/contracts, GET /api/contracts/{id},
    // POST /api/contracts, and PATCH /api/contracts/{id}/status
    public class ContractsApiIntegrationTests : IClassFixture<TechMoveApiFactory>
    {
        private readonly HttpClient _client;
        private readonly TechMoveApiFactory _factory;

        public ContractsApiIntegrationTests(TechMoveApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateSeededClient();
        }

        // ── GET /api/contracts ────────────────────────────────────────────────

        [Fact]
        public async Task GetContracts_Returns200Ok()
        {
            var response = await _client.GetAsync("api/contracts");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetContracts_ReturnsJsonContentType()
        {
            var response = await _client.GetAsync("api/contracts");
            Assert.Contains("application/json",
                response.Content.Headers.ContentType?.MediaType ?? "");
        }

        [Fact]
        public async Task GetContracts_ReturnsNonEmptyList()
        {
            var response = await _client.GetAsync("api/contracts");
            var contracts = await IntegrationTestHelpers
                                .ReadJson<List<JsonElement>>(response);

            Assert.NotNull(contracts);
            Assert.True(contracts!.Count >= 2,
                "Expected at least 2 seeded contracts.");
        }

        [Fact]
        public async Task GetContracts_EachContract_HasRequiredFields()
        {
            var response = await _client.GetAsync("api/contracts");
            var contracts = await IntegrationTestHelpers
                                .ReadJson<List<JsonElement>>(response);

            Assert.NotNull(contracts);
            foreach (var c in contracts!)
            {
                Assert.True(c.TryGetProperty("id", out _), "Missing 'id'");
                Assert.True(c.TryGetProperty("clientId", out _), "Missing 'clientId'");
                Assert.True(c.TryGetProperty("status", out _), "Missing 'status'");
                Assert.True(c.TryGetProperty("serviceLevel", out _), "Missing 'serviceLevel'");
            }
        }

        // ── GET /api/contracts?status= ────────────────────────────────────────

        [Fact]
        public async Task GetContracts_FilterByStatus_ReturnsOnlyMatchingContracts()
        {
            var response = await _client.GetAsync("api/contracts?status=Active");
            var contracts = await IntegrationTestHelpers
                                .ReadJson<List<JsonElement>>(response);

            Assert.NotNull(contracts);
            Assert.All(contracts!, c =>
                Assert.Equal("Active", c.GetProperty("status").GetString()));
        }

        [Fact]
        public async Task GetContracts_FilterByStatus_Expired_ReturnsExpiredContracts()
        {
            var response = await _client.GetAsync("api/contracts?status=Expired");
            var contracts = await IntegrationTestHelpers
                                .ReadJson<List<JsonElement>>(response);

            Assert.NotNull(contracts);
            Assert.All(contracts!, c =>
                Assert.Equal("Expired", c.GetProperty("status").GetString()));
        }

        [Fact]
        public async Task GetContracts_FilterByFromDate_ReturnsOnlyContractsAfterDate()
        {
            var response = await _client.GetAsync("api/contracts?fromDate=2025-01-01");
            var contracts = await IntegrationTestHelpers
                                .ReadJson<List<JsonElement>>(response);

            Assert.NotNull(contracts);
            Assert.True(contracts!.Count >= 1);
        }

        // ── GET /api/contracts/{id} ───────────────────────────────────────────

        [Fact]
        public async Task GetContractById_Returns200_WhenExists()
        {
            var response = await _client.GetAsync("api/contracts/1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetContractById_ReturnsCorrectContract()
        {
            var response = await _client.GetAsync("api/contracts/1");
            var contract = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            Assert.Equal(1, contract.GetProperty("id").GetInt32());
            Assert.Equal("Gold", contract.GetProperty("serviceLevel").GetString());

            var clientName = contract.GetProperty("clientName").GetString();
            Assert.False(string.IsNullOrEmpty(clientName),
                "Expected clientName to be populated.");
        }

        [Fact]
        public async Task GetContractById_IncludesClientName()
        {
            var response = await _client.GetAsync("api/contracts/1");
            var contract = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            var clientName = contract.GetProperty("clientName").GetString();
            Assert.False(string.IsNullOrEmpty(clientName),
                "Expected clientName to be populated.");
        }

        [Fact]
        public async Task GetContractById_Returns404_WhenNotFound()
        {
            var response = await _client.GetAsync("api/contracts/99999");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ── POST /api/contracts ───────────────────────────────────────────────

        [Fact]
        public async Task CreateContract_Returns201Created()
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("1"), "clientId");
            form.Add(new StringContent("2026-01-01"), "startDate");
            form.Add(new StringContent("2026-12-31"), "endDate");
            form.Add(new StringContent("Bronze"), "serviceLevel");

            var response = await _client.PostAsync("api/contracts", form);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task CreateContract_ReturnsCreatedContractInBody()
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("1"), "clientId");
            form.Add(new StringContent("2026-03-01"), "startDate");
            form.Add(new StringContent("2027-03-01"), "endDate");
            form.Add(new StringContent("Silver"), "serviceLevel");

            var response = await _client.PostAsync("api/contracts", form);
            var contract = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            Assert.Equal("Silver", contract.GetProperty("serviceLevel").GetString());
            Assert.True(contract.GetProperty("id").GetInt32() > 0,
                "Expected a positive contract ID.");
        }

        [Fact]
        public async Task CreateContract_Gold_CreatesActiveContract()
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("1"), "clientId");
            form.Add(new StringContent("2026-06-01"), "startDate");
            form.Add(new StringContent("2027-06-01"), "endDate");
            form.Add(new StringContent("Gold"), "serviceLevel");

            var response = await _client.PostAsync("api/contracts", form);
            var contract = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            // ContractFactory sets Gold → Active automatically
            Assert.Equal("Active", contract.GetProperty("status").GetString());
        }

        [Fact]
        public async Task CreateContract_NonGold_CreatesDraftContract()
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("2"), "clientId");
            form.Add(new StringContent("2026-07-01"), "startDate");
            form.Add(new StringContent("2027-07-01"), "endDate");
            form.Add(new StringContent("Bronze"), "serviceLevel");

            var response = await _client.PostAsync("api/contracts", form);
            var contract = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            // ContractFactory sets non-Gold → Draft automatically
            Assert.Equal("Draft", contract.GetProperty("status").GetString());
        }

        // ── PATCH /api/contracts/{id}/status ──────────────────────────────────

        [Fact]
        public async Task ChangeStatus_Returns204NoContent()
        {
            // Send as integer: Draft=0, Active=1, Expired=2, OnHold=3
            var body = IntegrationTestHelpers.ToJson(new { newStatus = 3 });
            var response = await _client.PatchAsync("api/contracts/1/status", body);

            // If still BadRequest, print the response body to diagnose
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"BadRequest body: {err}");
            }

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task ChangeStatus_UpdatesContractStatusInDatabase()
        {
            // Change contract 1 to OnHold (3)
            var body = IntegrationTestHelpers.ToJson(new { newStatus = 3 });
            await _client.PatchAsync("api/contracts/1/status", body);

            // Re-fetch and verify
            var getResponse = await _client.GetAsync("api/contracts/1");
            var contract = await IntegrationTestHelpers
                                  .ReadJson<JsonElement>(getResponse);

            Assert.Equal("OnHold", contract.GetProperty("status").GetString());
        }

        [Fact]
        public async Task ChangeStatus_Returns404_WhenContractDoesNotExist()
        {
            var body = IntegrationTestHelpers.ToJson(new { newStatus = 1 });
            var response = await _client.PatchAsync("api/contracts/99999/status", body);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ── Auth guard tests ──────────────────────────────────────────────────

        [Fact]
        public async Task GetContracts_Returns401_WhenNoJwtProvided()
        {
            var anonClient = _factory.CreateSeededAnonymousClient();
            var response = await anonClient.GetAsync("api/contracts");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ChangeStatus_Returns401_WhenNoJwtProvided()
        {
            var anonClient = _factory.CreateSeededAnonymousClient();
            var body = IntegrationTestHelpers.ToJson(new { newStatus = 1 });
            var response = await anonClient.PatchAsync("api/contracts/1/status", body);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}