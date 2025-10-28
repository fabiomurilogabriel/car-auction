using CarAuction.Domain.Abstractions.Requests;
using CarAuction.Domain.Abstractions.Results;
using CarAuction.Domain.Models.Auctions;

namespace CarAuction.Domain.Abstractions
{
    public interface IAuctionService
    {
        Task<Auction> CreateAuctionAsync(CreateAuctionRequest request);
        Task<BidResult> PlaceBidAsync(Guid auctionId, BidRequest request);
        Task<Auction> GetAuctionAsync(Guid auctionId, ConsistencyLevel consistency);
        Task<ReconciliationResult> ReconcileAuctionAsync(Guid auctionId);
    }
}
