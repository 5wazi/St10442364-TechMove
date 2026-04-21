using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using PROG7313_TechMove.Models;
using PROG7313_TechMove.Patterns.Factory;
using PROG7313_TechMove.Patterns.Observer;
using PROG7313_TechMove.Patterns.Repository;
using PROG7313_TechMove.Services;

namespace TestProject
{
    public class ContractServiceTests
    {
        // fixture 

        private static (ContractService svc,
                        Mock<IContractRepository> contractRepo,
                        Mock<IContractFactory> factory,
                        Mock<IFileService> fileService,
                        Mock<IContractObserver> observer)
            Build()
        {
            var contractRepo = new Mock<IContractRepository>();
            var factory = new Mock<IContractFactory>();
            var fileService = new Mock<IFileService>();
            var observer = new Mock<IContractObserver>();

            contractRepo.Setup(r => r.AddAsync(It.IsAny<Contract>())).Returns(Task.CompletedTask);
            contractRepo.Setup(r => r.UpdateAsync(It.IsAny<Contract>())).Returns(Task.CompletedTask);
            observer.Setup(o => o.OnContractStatusChangedAsync(It.IsAny<ContractStatusChangedEvent>()))
                    .Returns(Task.CompletedTask);

            var svc = new ContractService(
                contractRepo.Object,
                factory.Object,
                fileService.Object,
                new[] { observer.Object },
                Mock.Of<ILogger<ContractService>>());

            return (svc, contractRepo, factory, fileService, observer);
        }

        private static Contract MakeContract(int id, ContractStatus status = ContractStatus.Active) => new()
        {
            Id = id,
            ClientId = 1,
            ServiceLevel = "Gold",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddYears(1),
            Status = status
        };

        private static IFormFile MakePdf(string name = "agreement.pdf")
        {
            var bytes = Encoding.UTF8.GetBytes("fake-pdf");
            var stream = new MemoryStream(bytes);
            var file = new Mock<IFormFile>();
            file.Setup(f => f.FileName).Returns(name);
            file.Setup(f => f.Length).Returns(stream.Length);
            file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>((dest, _) => { stream.Position = 0; stream.CopyTo(dest); })
                .Returns(Task.CompletedTask);
            return file.Object;
        }

        // CreateContractAsync 

        [Fact]
        public async Task CreateContract_CallsFactory_WithCorrectArguments()
        {
            var (svc, _, factory, _, _) = Build();
            var start = new DateTime(2024, 1, 1);
            var end = new DateTime(2025, 1, 1);
            factory.Setup(f => f.CreateContract(1, start, end, "Gold")).Returns(MakeContract(0));

            await svc.CreateContractAsync(1, start, end, "Gold", null);
            factory.Verify(f => f.CreateContract(1, start, end, "Gold"), Times.Once);
        }

        [Fact]
        public async Task CreateContract_CallsRepositoryAddAsync()
        {
            var (svc, contractRepo, factory, _, _) = Build();
            factory.Setup(f => f.CreateContract(It.IsAny<int>(), It.IsAny<DateTime>(),
                                                 It.IsAny<DateTime>(), It.IsAny<string>()))
                   .Returns(MakeContract(0));

            await svc.CreateContractAsync(1, DateTime.Today, DateTime.Today.AddYears(1), "Gold", null);
            contractRepo.Verify(r => r.AddAsync(It.IsAny<Contract>()), Times.Once);
        }

        [Fact]
        public async Task CreateContract_DoesNotCallFileService_WhenFileIsNull()
        {
            var (svc, _, factory, fileService, _) = Build();
            factory.Setup(f => f.CreateContract(It.IsAny<int>(), It.IsAny<DateTime>(),
                                                 It.IsAny<DateTime>(), It.IsAny<string>()))
                   .Returns(MakeContract(0));

            await svc.CreateContractAsync(1, DateTime.Today, DateTime.Today.AddYears(1), "Gold", null);
            fileService.Verify(f => f.SaveSignedAgreementAsync(It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task CreateContract_CallsFileService_WhenPdfProvided()
        {
            var (svc, _, factory, fileService, _) = Build();
            factory.Setup(f => f.CreateContract(It.IsAny<int>(), It.IsAny<DateTime>(),
                                                 It.IsAny<DateTime>(), It.IsAny<string>()))
                   .Returns(MakeContract(0));
            fileService.Setup(f => f.SaveSignedAgreementAsync(It.IsAny<IFormFile>()))
                       .ReturnsAsync(("/uploads/agreements/guid.pdf", "agreement.pdf"));

            await svc.CreateContractAsync(1, DateTime.Today, DateTime.Today.AddYears(1), "Gold", MakePdf());
            fileService.Verify(f => f.SaveSignedAgreementAsync(It.IsAny<IFormFile>()), Times.Once);
        }

        [Fact]
        public async Task CreateContract_SetsSignedAgreementFields_WhenPdfProvided()
        {
            var (svc, _, factory, fileService, _) = Build();
            var contract = MakeContract(0);
            factory.Setup(f => f.CreateContract(It.IsAny<int>(), It.IsAny<DateTime>(),
                                                 It.IsAny<DateTime>(), It.IsAny<string>()))
                   .Returns(contract);
            fileService.Setup(f => f.SaveSignedAgreementAsync(It.IsAny<IFormFile>()))
                       .ReturnsAsync(("/uploads/agreements/test.pdf", "original.pdf"));

            var result = await svc.CreateContractAsync(1, DateTime.Today, DateTime.Today.AddYears(1), "Gold", MakePdf());
            Assert.Equal("/uploads/agreements/test.pdf", result.SignedAgreementPath);
            Assert.Equal("original.pdf", result.SignedAgreementFileName);
        }

        [Fact]
        public async Task CreateContract_LeavesSignedAgreementNull_WhenNoFile()
        {
            var (svc, _, factory, _, _) = Build();
            var contract = MakeContract(0);
            factory.Setup(f => f.CreateContract(It.IsAny<int>(), It.IsAny<DateTime>(),
                                                 It.IsAny<DateTime>(), It.IsAny<string>()))
                   .Returns(contract);

            var result = await svc.CreateContractAsync(1, DateTime.Today, DateTime.Today.AddYears(1), "Gold", null);
            Assert.Null(result.SignedAgreementPath);
            Assert.Null(result.SignedAgreementFileName);
        }

        // ChangeStatusAsync 

        [Fact]
        public async Task ChangeStatus_UpdatesContractStatus()
        {
            var (svc, contractRepo, _, _, _) = Build();
            var contract = MakeContract(1, ContractStatus.Draft);
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(contract);

            await svc.ChangeStatusAsync(1, ContractStatus.Active);
            Assert.Equal(ContractStatus.Active, contract.Status);
        }

        [Fact]
        public async Task ChangeStatus_CallsUpdateAsync()
        {
            var (svc, contractRepo, _, _, _) = Build();
            var contract = MakeContract(1, ContractStatus.Draft);
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(contract);

            await svc.ChangeStatusAsync(1, ContractStatus.Active);
            contractRepo.Verify(r => r.UpdateAsync(contract), Times.Once);
        }

        [Fact]
        public async Task ChangeStatus_Throws_WhenContractNotFound()
        {
            var (svc, contractRepo, _, _, _) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Contract?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => svc.ChangeStatusAsync(999, ContractStatus.Active));
        }

        [Theory]
        [InlineData(ContractStatus.Draft, ContractStatus.Active)]
        [InlineData(ContractStatus.Active, ContractStatus.Expired)]
        [InlineData(ContractStatus.Active, ContractStatus.OnHold)]
        [InlineData(ContractStatus.OnHold, ContractStatus.Active)]
        public async Task ChangeStatus_AllowsAllValidTransitions(ContractStatus from, ContractStatus to)
        {
            var (svc, contractRepo, _, _, _) = Build();
            var contract = MakeContract(1, from);
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(contract);

            await svc.ChangeStatusAsync(1, to);
            Assert.Equal(to, contract.Status);
        }

        // Observer notification 

        [Fact]
        public async Task ChangeStatus_NotifiesObserver_WithCorrectEvent()
        {
            var (svc, contractRepo, _, _, observer) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, ContractStatus.Draft));

            ContractStatusChangedEvent? captured = null;
            observer.Setup(o => o.OnContractStatusChangedAsync(It.IsAny<ContractStatusChangedEvent>()))
                    .Callback<ContractStatusChangedEvent>(e => captured = e)
                    .Returns(Task.CompletedTask);

            await svc.ChangeStatusAsync(1, ContractStatus.Active);

            Assert.NotNull(captured);
            Assert.Equal(1, captured!.ContractId);
            Assert.Equal(ContractStatus.Draft, captured.OldStatus);
            Assert.Equal(ContractStatus.Active, captured.NewStatus);
        }

        [Fact]
        public async Task ChangeStatus_NotifiesObserver_ExactlyOnce()
        {
            var (svc, contractRepo, _, _, observer) = Build();
            contractRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(MakeContract(1, ContractStatus.Draft));

            await svc.ChangeStatusAsync(1, ContractStatus.Active);
            observer.Verify(o => o.OnContractStatusChangedAsync(
                It.IsAny<ContractStatusChangedEvent>()), Times.Once);
        }

        // ContractFactory (unit tests for the real factory) 

        [Fact]
        public void ContractFactory_GoldServiceLevel_CreatesActiveContract()
        {
            var factory = new ContractFactory();
            var contract = factory.CreateContract(1, DateTime.Today, DateTime.Today.AddYears(1), "Gold");
            Assert.Equal(ContractStatus.Active, contract.Status);
        }

        [Fact]
        public void ContractFactory_SilverServiceLevel_CreatesDraftContract()
        {
            var factory = new ContractFactory();
            var contract = factory.CreateContract(1, DateTime.Today, DateTime.Today.AddYears(1), "Silver");
            Assert.Equal(ContractStatus.Draft, contract.Status);
        }

        [Fact]
        public void ContractFactory_SetsAllFieldsCorrectly()
        {
            var factory = new ContractFactory();
            var start = new DateTime(2024, 1, 1);
            var end = new DateTime(2025, 1, 1);
            var contract = factory.CreateContract(7, start, end, "Gold");

            Assert.Equal(7, contract.ClientId);
            Assert.Equal(start, contract.StartDate);
            Assert.Equal(end, contract.EndDate);
            Assert.Equal("Gold", contract.ServiceLevel);
        }

        // SearchContractsAsync 

        [Fact]
        public async Task SearchContracts_DelegatesToRepository()
        {
            var (svc, contractRepo, _, _, _) = Build();
            var data = new List<Contract> { MakeContract(1), MakeContract(2) };
            contractRepo.Setup(r => r.SearchAsync(null, null, null)).ReturnsAsync(data);

            var result = await svc.SearchContractsAsync(null, null, null);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task SearchContracts_PassesFilters_ToRepository()
        {
            var (svc, contractRepo, _, _, _) = Build();
            var from = new DateTime(2024, 1, 1);
            var to = new DateTime(2024, 12, 31);
            contractRepo.Setup(r => r.SearchAsync(from, to, ContractStatus.Active))
                        .ReturnsAsync(new List<Contract>());

            await svc.SearchContractsAsync(from, to, ContractStatus.Active);
            contractRepo.Verify(r => r.SearchAsync(from, to, ContractStatus.Active), Times.Once);
        }
    }
}