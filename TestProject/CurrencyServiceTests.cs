using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using PROG7313_TechMove.Services;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace TestProject
{
    public class CurrencyServiceTests
    {
        // ── helpers ───────────────────────────────────────────────────────────

        private static CurrencyService Build(HttpMessageHandler handler)
            => new CurrencyService(
                new HttpClient(handler),
                Mock.Of<ILogger<CurrencyService>>());

        private static HttpMessageHandler OkHandler(decimal zarRate)
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = JsonContent.Create(new
                    {
                        rates = new Dictionary<string, decimal> { ["ZAR"] = zarRate }
                    })
                });
            return mock.Object;
        }

        private static HttpMessageHandler ThrowingHandler()
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new Exception("API down"));
            return mock.Object;
        }

        private static HttpMessageHandler StatusHandler(HttpStatusCode code)
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(code)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });
            return mock.Object;
        }

        // ── GetUsdToZarRateAsync ──────────────────────────────────────────────

        [Fact]
        public async Task GetRate_ReturnsRateFromApi_WhenApiReturnsZar()
        {
            var svc = Build(OkHandler(18.75m));
            Assert.Equal(18.75m, await svc.GetUsdToZarRateAsync());
        }

        [Fact]
        public async Task GetUsdToZarRate_WhenApiFails_ShouldUseFallback()
        {
            // matches your original test name exactly
            var svc = Build(ThrowingHandler());
            Assert.Equal(18.50m, await svc.GetUsdToZarRateAsync());
        }

        [Fact]
        public async Task GetRate_ReturnsFallback_WhenZarKeyMissing()
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = JsonContent.Create(new
                    {
                        rates = new Dictionary<string, decimal> { ["EUR"] = 0.92m }
                    })
                });
            Assert.Equal(18.50m, await Build(mock.Object).GetUsdToZarRateAsync());
        }

        [Fact]
        public async Task GetRate_ReturnsFallback_WhenApi503()
        {
            var svc = Build(StatusHandler(HttpStatusCode.ServiceUnavailable));
            Assert.Equal(18.50m, await svc.GetUsdToZarRateAsync());
        }

        [Fact]
        public async Task GetRate_ReturnsFallback_WhenApiReturnsInvalidJson()
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("NOT_VALID_JSON", Encoding.UTF8, "application/json")
                });
            Assert.Equal(18.50m, await Build(mock.Object).GetUsdToZarRateAsync());
        }

        // ── ConvertUsdToZarAsync ──────────────────────────────────────────────

        [Fact]
        public async Task ConvertUsdToZar_ShouldCalculateCorrectly()
        {
            // original test — kept exactly
            var svc = Build(OkHandler(20.0m));
            var (zar, rate) = await svc.ConvertUsdToZarAsync(100m);
            Assert.Equal(2000m, zar);
            Assert.Equal(20m, rate);
        }

        [Fact]
        public async Task Convert_ZeroUsd_ReturnsZeroZar()
        {
            var (zar, _) = await Build(OkHandler(18.00m)).ConvertUsdToZarAsync(0m);
            Assert.Equal(0.00m, zar);
        }

        [Fact]
        public async Task Convert_OneUsd_ReturnsExactRate()
        {
            var (zar, _) = await Build(OkHandler(18.50m)).ConvertUsdToZarAsync(1m);
            Assert.Equal(18.50m, zar);
        }

        [Fact]
        public async Task Convert_SmallFractional_RoundsToTwoDecimalPlaces()
        {
            // 0.01 × 18.00 = 0.18
            var (zar, _) = await Build(OkHandler(18.00m)).ConvertUsdToZarAsync(0.01m);
            Assert.Equal(0.18m, zar);
        }

        [Fact]
        public async Task Convert_RateWithManyDecimals_RoundsZarCorrectly()
        {
            // 1 × 18.557 = 18.56
            var (zar, _) = await Build(OkHandler(18.557m)).ConvertUsdToZarAsync(1m);
            Assert.Equal(18.56m, zar);
        }

        [Fact]
        public async Task Convert_LargeAmount_IsCorrect()
        {
            var (zar, _) = await Build(OkHandler(20.00m)).ConvertUsdToZarAsync(1_000_000m);
            Assert.Equal(20_000_000.00m, zar);
        }

        [Fact]
        public async Task Convert_ReturnsRateUsed_MatchingFetchedRate()
        {
            var (_, rateUsed) = await Build(OkHandler(19.25m)).ConvertUsdToZarAsync(50m);
            Assert.Equal(19.25m, rateUsed);
        }

        [Fact]
        public async Task Convert_WhenApiFails_UsesFallbackRateForMath()
        {
            // 100 × 18.50 fallback = 1850
            var (zar, rate) = await Build(ThrowingHandler()).ConvertUsdToZarAsync(100m);
            Assert.Equal(18.50m, rate);
            Assert.Equal(1850.00m, zar);
        }
    }
}