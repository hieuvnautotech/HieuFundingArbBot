using FundingArbBot.Models;
using HieuFundingArbBot.Models;

namespace HieuFundingArbBot.Interfaces
{
    public interface IExchangeClient
    {
        string Name { get; }
        Task<FundingRate> GetFundingRateAsync(string symbol);
        // Later: Task<OrderResult> PlaceOrderAsync(...)

        // Orders
        Task<OrderResult> PlaceOrderAsync(OrderRequest req);
        Task<OrderResult> CancelOrderAsync(string orderId);
    }
}