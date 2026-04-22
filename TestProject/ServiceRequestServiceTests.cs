using Microsoft.Extensions.Logging;
using Moq;
using PROG7313_TechMove.Models;
using PROG7313_TechMove.Patterns.Repository;
using PROG7313_TechMove.Services;

namespace TestProject
{
    public class ServiceRequestServiceTests
    {
        // fixture 

        private static (ServiceRequestService svc,
                        Mock<IServiceRequestRepository> srRepo,
                        Mock<IContractRepository> contractRepo,
                        Mock<ICurrencyService> currency)
            Build(decimal rate = 18.00m)
        {
            var srRepo = new Mock<IServiceRequestRepository>();
            var contractRepo = new Mock<IContractRepository>();
            var currency = new Mock<ICurrencyService>();

            currency.Setup(c => c.ConvertUsdToZarAsync(It.IsAny<decimal>()))
                    .ReturnsAsync((decimal usd) => (Math.Round(usd * rate, 2), rate));

            srRepo.Setup(r => r.AddAsync(It.IsAny<ServiceRequest>()))
                  .Returns(Task.CompletedTask);

            var svc = new ServiceRequestService(
                srRepo.Object,
                contractRepo.Object,
                currency.Object,
                Mock.Of<ILogger<ServiceRequestService>>());

            return (svc, srRepo, contractRepo, currency);
        }

        private static Contract MakeContract(int id, ContractStatus status) => new()
        {
            Id = id,
            ClientId = 1,
            ServiceLevel = "Gold",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddYears(1),
            Status = status
        };

        // Calls CreateAsync and captures the ServiceRequest passed to AddAsync
        private static async Task<ServiceRequest> CreateAndCapture(
            ServiceRequestService svc,
            Mock<IServiceRequestRepository> srRepo,
            int contractId = 1,
            string desc = "Test",
            decimal usd = 100m)
        {
            ServiceRequest? saved = null;
            srRepo.Setup(r => r.AddAsync(It.IsAny<ServiceRequest>()))
                  .Callback<ServiceRequest>(sr => saved = sr)
                  .Returns(Task.CompletedTask);

            await svc.CreateAsync(contractId, desc, usd);
            return saved!;
        }

        // workflow: blocked statuses 

        [Theory]
        [InlineData(ContractStatus.Expired)]
        [InlineData(ContractStatus.OnHold)]
        public async Task Create_Throws_WhenContractStatusIsBlocked(ContractStatus blocked)
        {
            var (svc, _, contractRepo, _) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, blocked));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.CreateAsync(1, "Freight", 50m));

            Assert.Contains(blocked.ToString(), ex.Message);
            Assert.Contains("Only Active or Draft", ex.Message);
        }

        [Theory]
        [InlineData(ContractStatus.Active)]
        [InlineData(ContractStatus.Draft)]
        public async Task Create_Succeeds_WhenContractStatusIsAllowed(ContractStatus allowed)
        {
            var (svc, _, contractRepo, _) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, allowed));
            Assert.NotNull(await svc.CreateAsync(1, "Freight", 50m));
        }

        // contract not found 

        [Fact]
        public async Task Create_Throws_WhenContractDoesNotExist()
        {
            var (svc, _, contractRepo, _) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Contract?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => svc.CreateAsync(99, "Test", 50m));
        }

        // currency math

        [Fact]
        public async Task Create_CostZar_IsUsdMultipliedByRate()
        {
            // 150 × 20 = 3000
            var (svc, srRepo, contractRepo, _) = Build(rate: 20.00m);
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, ContractStatus.Active));

            var saved = await CreateAndCapture(svc, srRepo, usd: 150m);
            Assert.Equal(3000.00m, saved.CostZar);
        }

        [Fact]
        public async Task Create_ExchangeRateUsed_MatchesCurrencyServiceRate()
        {
            var (svc, srRepo, contractRepo, _) = Build(rate: 19.42m);
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, ContractStatus.Active));

            var saved = await CreateAndCapture(svc, srRepo, usd: 200m);
            Assert.Equal(19.42m, saved.ExchangeRateUsed);
        }

        // edge cases 

        [Fact]
        public async Task Create_ZeroUsd_StoresZeroZar()
        {
            var (svc, srRepo, contractRepo, _) = Build(18.00m);
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, ContractStatus.Active));

            var saved = await CreateAndCapture(svc, srRepo, usd: 0m);
            Assert.Equal(0.00m, saved.CostZar);
        }

        [Fact]
        public async Task Create_MinimalUsd_ConvertsCorrectly()
        {
            // 0.01 × 18.00 = 0.18
            var (svc, srRepo, contractRepo, _) = Build(18.00m);
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, ContractStatus.Active));

            var saved = await CreateAndCapture(svc, srRepo, usd: 0.01m);
            Assert.Equal(0.18m, saved.CostZar);
        }

        // persisted fields 

        [Fact]
        public async Task Create_SavedRequest_HasPendingStatus()
        {
            var (svc, srRepo, contractRepo, _) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, ContractStatus.Active));

            var saved = await CreateAndCapture(svc, srRepo);
            Assert.Equal(ServiceRequestStatus.Pending, saved.Status);
        }

        [Fact]
        public async Task Create_SavedRequest_HasCorrectDescription()
        {
            var (svc, srRepo, contractRepo, _) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, ContractStatus.Active));

            var saved = await CreateAndCapture(svc, srRepo, desc: "Urgent cold chain");
            Assert.Equal("Urgent cold chain", saved.Description);
        }

        [Fact]
        public async Task Create_SavedRequest_HasCorrectContractId()
        {
            var (svc, srRepo, contractRepo, _) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(MakeContract(5, ContractStatus.Active));

            var saved = await CreateAndCapture(svc, srRepo, contractId: 5);
            Assert.Equal(5, saved.ContractId);
        }

        [Fact]
        public async Task Create_SavedRequest_HasCorrectCostUsd()
        {
            var (svc, srRepo, contractRepo, _) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, ContractStatus.Active));

            var saved = await CreateAndCapture(svc, srRepo, usd: 250m);
            Assert.Equal(250m, saved.CostUsd);
        }

        [Fact]
        public async Task Create_SavedRequest_CreatedAtIsUtcNow()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var (svc, srRepo, contractRepo, _) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, ContractStatus.Active));

            var saved = await CreateAndCapture(svc, srRepo);
            Assert.InRange(saved.CreatedAt, before, DateTime.UtcNow.AddSeconds(1));
        }

        // repository call counts 

        [Fact]
        public async Task Create_CallsAddAsync_ExactlyOnce_OnSuccess()
        {
            var (svc, srRepo, contractRepo, _) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, ContractStatus.Active));

            await svc.CreateAsync(1, "Test", 50m);
            srRepo.Verify(r => r.AddAsync(It.IsAny<ServiceRequest>()), Times.Once);
        }

        [Fact]
        public async Task Create_NeverCallsAddAsync_WhenContractIsExpired()
        {
            var (svc, srRepo, contractRepo, _) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, ContractStatus.Expired));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.CreateAsync(1, "Test", 50m));
            srRepo.Verify(r => r.AddAsync(It.IsAny<ServiceRequest>()), Times.Never);
        }

        [Fact]
        public async Task Create_NeverCallsAddAsync_WhenContractIsOnHold()
        {
            var (svc, srRepo, contractRepo, _) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, ContractStatus.OnHold));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.CreateAsync(1, "Test", 50m));
            srRepo.Verify(r => r.AddAsync(It.IsAny<ServiceRequest>()), Times.Never);
        }
    }
}