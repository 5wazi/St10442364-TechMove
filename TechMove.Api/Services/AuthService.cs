using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TechMove.Api.DTOs;

namespace TechMove.Api.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    // Firebase ID-token verification + internal JWT issuance
    //
    // Flow:
    //   1. Client logs in with Firebase (email/password, Google, etc.)
    //   2. Client POSTs the Firebase ID token to POST /api/auth/login
    //   3. API validates the token against Firebase's public keys
    //   4. API mints a short-lived internal JWT used on every subsequent call
    //   5. Every protected endpoint validates that internal JWT via [Authorize]
    // ─────────────────────────────────────────────────────────────────────────

    public interface IAuthService
    {
        /// <summary>
        /// Verifies a Firebase ID token and returns an internal JWT + user info.
        /// Throws UnauthorizedAccessException when the Firebase token is invalid.
        /// </summary>
        Task<AuthResponseDto> ExchangeFirebaseTokenAsync(string firebaseIdToken);
    }

    public class AuthService : IAuthService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private readonly ILogger<AuthService> _logger;

        // Firebase public-key endpoint – same as the Firebase Admin SDK uses
        private const string FirebaseJwkUri =
            "https://www.googleapis.com/service_accounts/v1/jwk/securetoken@system.gserviceaccount.com";

        public AuthService(IConfiguration config, HttpClient httpClient, ILogger<AuthService> logger)
        {
            _config = config;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<AuthResponseDto> ExchangeFirebaseTokenAsync(string firebaseIdToken)
        {
            // ── Step 1: validate Firebase ID token ────────────────────────────
            var projectId = _config["Firebase:ProjectId"]
                ?? throw new InvalidOperationException("Firebase:ProjectId is not configured.");

            // Fetch Firebase public keys (JWKS)
            var jwks = await _httpClient.GetStringAsync(FirebaseJwkUri);
            var keySet = new JsonWebKeySet(jwks);

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"https://securetoken.google.com/{projectId}",
                ValidateAudience = true,
                ValidAudience = projectId,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keySet.Keys,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            ClaimsPrincipal principal;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                principal = handler.ValidateToken(firebaseIdToken, validationParams, out _);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Firebase token validation failed: {Message}", ex.Message);
                throw new UnauthorizedAccessException("Invalid or expired Firebase ID token.");
            }

            // ── Step 2: extract claims ────────────────────────────────────────
            var uid = principal.FindFirstValue("user_id")
                     ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? "";
            var email = principal.FindFirstValue("email")
                     ?? principal.FindFirstValue(ClaimTypes.Email)
                     ?? "";

            // ── Step 3: mint internal JWT ─────────────────────────────────────
            return MintInternalJwt(uid, email);
        }

        // ── Internal JWT helpers ──────────────────────────────────────────────

        private AuthResponseDto MintInternalJwt(string uid, string email)
        {
            var jwtSettings = _config.GetSection("Jwt");

            var keyString = jwtSettings["Key"]
                ?? throw new InvalidOperationException("Jwt:Key is not configured.");

            var issuer = jwtSettings["Issuer"] ?? "TechMove.Api";
            var audience = jwtSettings["Audience"] ?? "TechMove.Web";
            var expiry = DateTime.UtcNow.AddHours(
                               double.Parse(jwtSettings["ExpiryHours"] ?? "8"));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
        new Claim(JwtRegisteredClaimNames.Sub,   uid),
        new Claim(JwtRegisteredClaimNames.Email, email),
        new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        new Claim("uid", uid)
    };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiry,
                signingCredentials: creds);

            return new AuthResponseDto
            {
                Jwt = new JwtSecurityTokenHandler().WriteToken(token),
                Email = email,
                Uid = uid,
                ExpiresAt = expiry
            };
        }
    }
}