using System.Net;
using System.Text.Json;
using TestProject.Integration;

namespace TestProject.Integration
{
   
    // GET /api/servicerequests/by-contract/{id}, and GET /api/servicerequests/rate
    public class ServiceRequestsApiIntegrationTests : IClassFixture<TechMoveApiFactory>
    {
        private readonly HttpClient _client;
        private readonly TechMoveApiFactory _factory;

        public ServiceRequestsApiIntegrationTests(TechMoveApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateSeededClient();
        }

        // ── GET /api/servicerequests/rate ─────────────────────────────────────

        [Fact]
        public async Task GetRate_Returns200Ok()
        {
            var response = await _client.GetAsync("api/servicerequests/rate");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetRate_ReturnsJsonContentType()
        {
            var response = await _client.GetAsync("api/servicerequests/rate");
            Assert.Contains("application/json",
                response.Content.Headers.ContentType?.MediaType ?? "");
        }

        [Fact]
        public async Task GetRate_ReturnsPositiveRate()
        {
            var response = await _client.GetAsync("api/servicerequests/rate");
            var body     = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            var rate = body.GetProperty("rate").GetDecimal();
            Assert.True(rate > 0, $"Expected rate > 0 but got {rate}.");
        }

        [Fact]
        public async Task GetRate_ReturnsRateWithExpectedFields()
        {
            var response = await _client.GetAsync("api/servicerequests/rate");
            var body     = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            Assert.True(body.TryGetProperty("rate",      out _), "Missing 'rate'");
            Assert.True(body.TryGetProperty("base",      out _), "Missing 'base'");
            Assert.True(body.TryGetProperty("target",    out _), "Missing 'target'");
            Assert.True(body.TryGetProperty("fetchedAt", out _), "Missing 'fetchedAt'");
        }

        [Fact]
        public async Task GetRate_BaseIsUsd_TargetIsZar()
        {
            var response = await _client.GetAsync("api/servicerequests/rate");
            var body     = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            Assert.Equal("USD", body.GetProperty("base").GetString());
            Assert.Equal("ZAR", body.GetProperty("target").GetString());
        }

        // ── POST /api/servicerequests ─────────────────────────────────────────

        [Fact]
        public async Task CreateServiceRequest_Returns201_ForActiveContract()
        {
            // Contract 1 is Active (seeded in factory)
            var body = IntegrationTestHelpers.ToJson(new
            {
                contractId  = 1,
                description = "Refrigerated container transport — Cape Town to Durban",
                costUsd     = 150.00m
            });

            var response = await _client.PostAsync("api/servicerequests", body);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task CreateServiceRequest_ReturnsCreatedRequestInBody()
        {
            var body = IntegrationTestHelpers.ToJson(new
            {
                contractId  = 1,
                description = "Customs clearance documentation",
                costUsd     = 75.50m
            });

            var response = await _client.PostAsync("api/servicerequests", body);
            var sr       = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            Assert.Equal(1,      sr.GetProperty("contractId").GetInt32());
            Assert.Equal(75.50m, sr.GetProperty("costUsd").GetDecimal());
            Assert.Equal("Customs clearance documentation",
                sr.GetProperty("description").GetString());
        }

        [Fact]
        public async Task CreateServiceRequest_StatusIsPending_OnCreation()
        {
            var body = IntegrationTestHelpers.ToJson(new
            {
                contractId  = 1,
                description = "Bulk cargo loading assistance",
                costUsd     = 200.00m
            });

            var response = await _client.PostAsync("api/servicerequests", body);
            var sr       = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            Assert.Equal("Pending", sr.GetProperty("status").GetString());
        }

        [Fact]
        public async Task CreateServiceRequest_CostZar_IsGreaterThanZero()
        {
            var body = IntegrationTestHelpers.ToJson(new
            {
                contractId  = 1,
                description = "Port handling fees",
                costUsd     = 100.00m
            });

            var response = await _client.PostAsync("api/servicerequests", body);
            var sr       = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            var costZar = sr.GetProperty("costZar").GetDecimal();
            Assert.True(costZar > 0,
                $"Expected costZar > 0 but got {costZar}.");
        }

        [Fact]
        public async Task CreateServiceRequest_ExchangeRateUsed_IsPositive()
        {
            var body = IntegrationTestHelpers.ToJson(new
            {
                contractId  = 1,
                description = "Freight insurance processing",
                costUsd     = 50.00m
            });

            var response = await _client.PostAsync("api/servicerequests", body);
            var sr       = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            var rate = sr.GetProperty("exchangeRateUsed").GetDecimal();
            Assert.True(rate > 0, $"Expected exchangeRateUsed > 0 but got {rate}.");
        }

        [Fact]
        public async Task CreateServiceRequest_Returns400_ForExpiredContract()
        {
            // Contract 2 is Expired (seeded in factory)
            var body = IntegrationTestHelpers.ToJson(new
            {
                contractId  = 2,
                description = "This should be blocked",
                costUsd     = 100.00m
            });

            var response = await _client.PostAsync("api/servicerequests", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateServiceRequest_Returns400_ErrorMessage_MentionsExpired()
        {
            var body = IntegrationTestHelpers.ToJson(new
            {
                contractId  = 2,
                description = "Should fail",
                costUsd     = 10.00m
            });

            var response = await _client.PostAsync("api/servicerequests", body);
            var bodyStr  = await response.Content.ReadAsStringAsync();

            Assert.Contains("Expired", bodyStr,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateServiceRequest_Returns404_WhenContractDoesNotExist()
        {
            var body = IntegrationTestHelpers.ToJson(new
            {
                contractId  = 99999,
                description = "No such contract",
                costUsd     = 100.00m
            });

            var response = await _client.PostAsync("api/servicerequests", body);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateServiceRequest_Returns400_WhenDescriptionIsMissing()
        {
            var body = IntegrationTestHelpers.ToJson(new
            {
                contractId = 1,
                costUsd    = 100.00m
                // description deliberately omitted
            });

            var response = await _client.PostAsync("api/servicerequests", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateServiceRequest_Returns400_WhenCostIsZero()
        {
            var body = IntegrationTestHelpers.ToJson(new
            {
                contractId  = 1,
                description = "Zero cost request",
                costUsd     = 0m
            });

            var response = await _client.PostAsync("api/servicerequests", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ── GET /api/servicerequests/by-contract/{contractId} ─────────────────

        [Fact]
        public async Task GetByContract_Returns200Ok()
        {
            // Create a request first so there is something to fetch
            var createBody = IntegrationTestHelpers.ToJson(new
            {
                contractId  = 1,
                description = "Route planning for by-contract test",
                costUsd     = 80.00m
            });
            await _client.PostAsync("api/servicerequests", createBody);

            var response = await _client.GetAsync(
                "api/servicerequests/by-contract/1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetByContract_ReturnsListOfRequests()
        {
            var createBody = IntegrationTestHelpers.ToJson(new
            {
                contractId  = 1,
                description = "Warehouse storage fees",
                costUsd     = 45.00m
            });
            await _client.PostAsync("api/servicerequests", createBody);

            var response  = await _client.GetAsync(
                "api/servicerequests/by-contract/1");
            var requests  = await IntegrationTestHelpers
                                .ReadJson<List<JsonElement>>(response);

            Assert.NotNull(requests);
            Assert.True(requests!.Count >= 1,
                "Expected at least one service request for contract 1.");
        }

        [Fact]
        public async Task GetByContract_AllRequests_BelongToCorrectContract()
        {
            var response = await _client.GetAsync(
                "api/servicerequests/by-contract/1");
            var requests = await IntegrationTestHelpers
                               .ReadJson<List<JsonElement>>(response);

            Assert.NotNull(requests);
            Assert.All(requests!, sr =>
                Assert.Equal(1, sr.GetProperty("contractId").GetInt32()));
        }

        // ── GET /api/servicerequests/{id} ─────────────────────────────────────

        [Fact]
        public async Task GetServiceRequestById_Returns200_WhenExists()
        {
            // Create one first so we have a known ID
            var createBody = IntegrationTestHelpers.ToJson(new
            {
                contractId  = 1,
                description = "Get-by-id test request",
                costUsd     = 60.00m
            });
            var createResp = await _client.PostAsync("api/servicerequests", createBody);
            var created    = await IntegrationTestHelpers
                                 .ReadJson<JsonElement>(createResp);
            var id         = created.GetProperty("id").GetInt32();

            var response = await _client.GetAsync($"api/servicerequests/{id}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetServiceRequestById_Returns404_WhenNotFound()
        {
            var response = await _client.GetAsync("api/servicerequests/99999");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ── Auth guard tests ──────────────────────────────────────────────────

        [Fact]
        public async Task GetRate_Returns401_WhenNoJwtProvided()
        {
            var anonClient = _factory.CreateSeededAnonymousClient();
            var response = await anonClient.GetAsync("api/servicerequests/rate");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateServiceRequest_Returns401_WhenNoJwtProvided()
        {
            var anonClient = _factory.CreateSeededAnonymousClient();
            var body = IntegrationTestHelpers.ToJson(new
            {
                contractId = 1,
                description = "Unauthorized test",
                costUsd = 50.00m
            });
            var response = await anonClient.PostAsync("api/servicerequests", body);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}