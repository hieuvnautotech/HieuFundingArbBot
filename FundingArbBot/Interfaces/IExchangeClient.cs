using HieuFundingArbBot.Models;

namespace HieuFundingArbBot.Interfaces
{
    public interface IExchangeClient
    {
        string Name { get; }
        Task<FundingRate> GetFundingRateAsync(string symbol);
        // Later: Task<OrderResult> PlaceOrderAsync(...)
    }
}