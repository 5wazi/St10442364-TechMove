namespace PROG7313_TechMove.Services
{
    public interface ICurrencyService
    {
        /// <summary>Returns the current USD → ZAR exchange rate.</summary>
        Task<decimal> GetUsdToZarRateAsync();

        /// <summary>Converts a USD amount to ZAR using the live rate.</summary>
        Task<(decimal zarAmount, decimal rateUsed)> ConvertUsdToZarAsync(decimal usdAmount);
    }

    /// <summary>
    /// Consumes the ExchangeRate-API (https://open.er-api.com) using HttpClient + Async/Await.
    /// Includes error handling: falls back to a cached/default rate if the API is unreachable.
    /// Rubric criterion 3: External API integration, HttpClient, Async/Await.
    /// </summary>
    public class CurrencyService : ICurrencyService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CurrencyService> _logger;
        private const decimal FallbackRate = 18.50m;   // sensible ZAR fallback
        private const string ApiUrl = "https://open.er-api.com/v6/latest/USD";

        public CurrencyService(HttpClient httpClient, ILogger<CurrencyService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<decimal> GetUsdToZarRateAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>(ApiUrl);
                if (response?.Rates != null && response.Rates.TryGetValue("ZAR", out var rate))
                    return rate;

                _logger.LogWarning("ZAR rate not found in API response. Using fallback rate {Rate}.", FallbackRate);
                return FallbackRate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Currency API unreachable. Using fallback rate {Rate}.", FallbackRate);
                return FallbackRate;
            }
        }

        public async Task<(decimal zarAmount, decimal rateUsed)> ConvertUsdToZarAsync(decimal usdAmount)
        {
            var rate = await GetUsdToZarRateAsync();
            var zarAmount = Math.Round(usdAmount * rate, 2);
            return (zarAmount, rate);
        }

        // DTO matching the open.er-api.com JSON response shape
        private class ExchangeRateResponse
        {
            public Dictionary<string, decimal>? Rates { get; set; }
        }
    }
}
