using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TechMove.Api.Data;
using TechMove.Api.Models;

using MvcTestingFactory =
    Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>;

namespace TestProject.Integration
{
    public class TechMoveApiFactory : MvcTestingFactory
    {
        private readonly string _dbName =
            "TechMoveTestDb_" + Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "DataSource=:memory:");
            builder.UseSetting("Jwt:Key",
                IntegrationTestHelpers.TestJwtKey);
            builder.UseSetting("Jwt:Issuer", "TechMove.Api");
            builder.UseSetting("Jwt:Audience", "TechMove.Web");
            builder.UseSetting("Jwt:ExpiryHours", "8");

            builder.ConfigureServices(services =>
            {
                // Remove ALL descriptors related to ApplicationDbContext
                // including the internal EF Core service provider
                var descriptorsToRemove = services
                    .Where(d =>
                        d.ServiceType.FullName != null &&
                        (d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                         d.ServiceType == typeof(DbContextOptions) ||
                         d.ServiceType == typeof(ApplicationDbContext) ||
                         d.ServiceType.FullName.StartsWith(
                             "Microsoft.EntityFrameworkCore") &&
                         d.ServiceType.FullName.Contains("SqlServer")))
                    .ToList();

                foreach (var d in descriptorsToRemove)
                    services.Remove(d);

                // Use a new InternalServiceProvider for InMemory
                // This prevents any SqlServer services from leaking in
                var serviceProvider = new ServiceCollection()
                    .AddEntityFrameworkInMemoryDatabase()
                    .BuildServiceProvider();

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_dbName);
                    options.UseInternalServiceProvider(serviceProvider);
                });
            });
        }

        public HttpClient CreateSeededClient(string? jwt = null)
        {
            var client = ((MvcTestingFactory)this).CreateClient();

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider
                          .GetRequiredService<ApplicationDbContext>();
            SeedDatabase(db);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    jwt ?? IntegrationTestHelpers.MintTestJwt());

            return client;
        }

        public HttpClient CreateSeededAnonymousClient()
        {
            var client = ((MvcTestingFactory)this).CreateClient();

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider
                          .GetRequiredService<ApplicationDbContext>();
            SeedDatabase(db);

            return client;
        }

        private static void SeedDatabase(ApplicationDbContext db)
        {
            if (db.Clients.Any()) return;

            db.Clients.AddRange(
                new Client
                {
                    Id = 1,
                    Name = "TransAfrica Logistics",
                    ContactDetails = "contact@transafrica.co.za",
                    Region = "Africa"
                },
                new Client
                {
                    Id = 2,
                    Name = "Nordic Freight Solutions",
                    ContactDetails = "info@nordicfreight.se",
                    Region = "Europe"
                }
            );

            db.Contracts.AddRange(
                new Contract
                {
                    Id = 1,
                    ClientId = 1,
                    StartDate = new DateTime(2025, 1, 1),
                    EndDate = new DateTime(2026, 12, 31),
                    Status = ContractStatus.Active,
                    ServiceLevel = "Gold"
                },
                new Contract
                {
                    Id = 2,
                    ClientId = 2,
                    StartDate = new DateTime(2024, 6, 1),
                    EndDate = new DateTime(2025, 5, 31),
                    Status = ContractStatus.Expired,
                    ServiceLevel = "Silver"
                }
            );

            db.SaveChanges();
        }
    }

    public static class IntegrationTestHelpers
    {
        public const string TestJwtKey =
            "TechMove_Super_Secret_JWT_Key_2026_Must_Be_At_Least_32_Chars!";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static string MintTestJwt(
            string uid = "test-user",
            string email = "test@techmove.com")
        {
            var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                          Encoding.UTF8.GetBytes(TestJwtKey));

            var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                key,
                Microsoft.IdentityModel.Tokens
                         .SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new System.Security.Claims.Claim("sub",   uid),
                new System.Security.Claims.Claim("email", email),
                new System.Security.Claims.Claim("jti",   Guid.NewGuid().ToString()),
                new System.Security.Claims.Claim("uid",   uid)
            };

            var token = new System.IdentityModel.Tokens.Jwt
                            .JwtSecurityToken(
                issuer: "TechMove.Api",
                audience: "TechMove.Web",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds);

            return new System.IdentityModel.Tokens.Jwt
                       .JwtSecurityTokenHandler()
                       .WriteToken(token);
        }

        public static async Task<T?> ReadJson<T>(HttpResponseMessage response)
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }

        public static StringContent ToJson(object obj) =>
            new(JsonSerializer.Serialize(obj),
                Encoding.UTF8,
                "application/json");
    }
}