using Microsoft.AspNetCore.Mvc;
using TechMove.Api.DTOs;
using TechMove.Api.Services;

namespace TechMove.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Exchange a Firebase ID token for an internal JWT.
        /// The client logs in via the Firebase SDK, then POSTs the resulting
        /// idToken here. The API validates it against Firebase's public keys
        /// and returns a short-lived JWT for subsequent API calls.
        /// </summary>
        /// <param name="dto">Firebase ID token</param>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] FirebaseTokenDto dto)
        {
            try
            {
                var result = await _authService.ExchangeFirebaseTokenAsync(dto.IdToken);
                _logger.LogInformation("User {Email} authenticated.", result.Email);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }
    }
}