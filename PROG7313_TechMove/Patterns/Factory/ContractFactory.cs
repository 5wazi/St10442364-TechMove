using PROG7313_TechMove.Models;


namespace PROG7313_TechMove.Patterns.Factory
{
    public class ContractFactory : IContractFactory
    {
        public Contract CreateContract(int clientId, DateTime startDate, DateTime endDate, string serviceLevel)
        {
            var status = serviceLevel.Equals("Gold", StringComparison.OrdinalIgnoreCase)
                ? ContractStatus.Active
                : ContractStatus.Draft;

            return new Contract
            {
                ClientId = clientId,
                StartDate = startDate,
                EndDate = endDate,
                ServiceLevel = serviceLevel,
                Status = status
            };
        }
    }
}
