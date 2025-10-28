using CarAuction.Domain.Abstractions.Results;
using CarAuction.Domain.Models.Bids;

namespace CarAuction.Domain.Abstractions
{
    public interface IBidOrderingService
    {
        Task<long> GetNextBidSequenceAsync(Guid auctionId);
        Task<BidAcceptance> ValidateBidOrderAsync(Guid auctionId, Bid bid);
        Task<IEnumerable<Bid>> GetOrderedBidsAsync(Guid auctionId, DateTime? since = null);
    }
}
