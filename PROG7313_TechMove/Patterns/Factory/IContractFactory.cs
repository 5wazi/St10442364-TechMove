using PROG7313_TechMove.Models;

namespace PROG7313_TechMove.Patterns.Factory
{
    public interface IContractFactory
    {
        Contract CreateContract(int clientId, DateTime startDate, DateTime endDate, string serviceLevel);
    }
}
