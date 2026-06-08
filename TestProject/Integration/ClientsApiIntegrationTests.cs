using System.Net;
using System.Text.Json;
using TestProject.Integration;

namespace TestProject.Integration
{
    
    public class ClientsApiIntegrationTests : IClassFixture<TechMoveApiFactory>
    {
        private readonly HttpClient _client;
        private readonly TechMoveApiFactory _factory;

        public ClientsApiIntegrationTests(TechMoveApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateSeededClient();
        }

        // ── GET /api/clients ──────────────────────────────────────────────────

        [Fact]
        public async Task GetClients_Returns200Ok()
        {
            var response = await _client.GetAsync("api/clients");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetClients_ReturnsJsonContentType()
        {
            var response = await _client.GetAsync("api/clients");
            Assert.Contains("application/json",
                response.Content.Headers.ContentType?.MediaType ?? "");
        }

        [Fact]
        public async Task GetClients_ReturnsNonNullBody()
        {
            var response = await _client.GetAsync("api/clients");
            var body = await response.Content.ReadAsStringAsync();
            Assert.NotNull(body);
            Assert.NotEmpty(body);
        }

        [Fact]
        public async Task GetClients_ReturnsSeededClients()
        {
            var response = await _client.GetAsync("api/clients");
            var clients = await IntegrationTestHelpers
                               .ReadJson<List<JsonElement>>(response);

            Assert.NotNull(clients);
            Assert.True(clients!.Count >= 2,
                "Expected at least 2 seeded clients.");
        }

        [Fact]
        public async Task GetClients_EachClient_HasRequiredFields()
        {
            var response = await _client.GetAsync("api/clients");
            var clients = await IntegrationTestHelpers
                               .ReadJson<List<JsonElement>>(response);

            Assert.NotNull(clients);
            foreach (var c in clients!)
            {
                Assert.True(c.TryGetProperty("id", out _), "Missing 'id'");
                Assert.True(c.TryGetProperty("name", out _), "Missing 'name'");
            }
        }

        // ── GET /api/clients/{id} ─────────────────────────────────────────────

        [Fact]
        public async Task GetClientById_Returns200_WhenClientExists()
        {
            var response = await _client.GetAsync("api/clients/1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetClientById_ReturnsCorrectClient()
        {
            var response = await _client.GetAsync("api/clients/1");
            var client = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            Assert.Equal(1, client.GetProperty("id").GetInt32());
            Assert.Equal("TransAfrica Logistics",
                client.GetProperty("name").GetString());
        }

        [Fact]
        public async Task GetClientById_Returns404_WhenClientDoesNotExist()
        {
            var response = await _client.GetAsync("api/clients/99999");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ── POST /api/clients ─────────────────────────────────────────────────

        [Fact]
        public async Task CreateClient_Returns201Created()
        {
            var body = IntegrationTestHelpers.ToJson(new
            {
                name = "Atlantic Cargo Lines",
                contactDetails = "support@atlanticcargo.com | +1 212 555 9812",
                region = "Americas"
            });

            var response = await _client.PostAsync("api/clients", body);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task CreateClient_ReturnsCreatedClientInBody()
        {
            var body = IntegrationTestHelpers.ToJson(new
            {
                name = "Pacific Shipping Co",
                contactDetails = "ops@pacshipping.com",
                region = "Asia Pacific"
            });

            var response = await _client.PostAsync("api/clients", body);
            var created = await IntegrationTestHelpers
                               .ReadJson<JsonElement>(response);

            Assert.Equal("Pacific Shipping Co",
                created.GetProperty("name").GetString());
            Assert.Equal("Asia Pacific",
                created.GetProperty("region").GetString());
        }

        [Fact]
        public async Task CreateClient_Returns400_WhenNameIsMissing()
        {
            var body = IntegrationTestHelpers.ToJson(new
            {
                contactDetails = "some@email.com",
                region = "Africa"
                // name deliberately omitted
            });

            var response = await _client.PostAsync("api/clients", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateClient_Returns400_WhenRegionIsMissing()
        {
            var body = IntegrationTestHelpers.ToJson(new
            {
                name = "Test Client",
                contactDetails = "test@test.com"
                // region deliberately omitted
            });

            var response = await _client.PostAsync("api/clients", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ── Auth guard tests ──────────────────────────────────────────────────

        [Fact]
        public async Task GetClients_Returns401_WhenNoJwtProvided()
        {
            var anonClient = _factory.CreateSeededAnonymousClient();
            var response = await anonClient.GetAsync("api/clients");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateClient_Returns401_WhenNoJwtProvided()
        {
            var anonClient = _factory.CreateSeededAnonymousClient();
            var body = IntegrationTestHelpers.ToJson(new
            {
                name = "Ghost Corp",
                contactDetails = "ghost@corp.com",
                region = "Europe"
            });
            var response = await anonClient.PostAsync("api/clients", body);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}