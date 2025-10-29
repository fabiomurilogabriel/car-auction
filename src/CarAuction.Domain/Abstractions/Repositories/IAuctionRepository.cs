using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Auctions;

namespace CarAuction.Domain.Abstractions.Repositories
{
    public interface IAuctionRepository
    {
        Task<Auction> GetByIdAsync(Guid id);
        Task<IEnumerable<Auction>> GetActiveAuctionsByRegionAsync(Region region);
        Task<Guid> CreateAsync(Auction auction);
        Task<bool> UpdateAsync(Auction auction);
        Task<Auction> GetWithBidsAsync(Guid id);
        Task<IEnumerable<Auction>> GetAuctionsThatNeedsReconciliationByRegionAsync(Region region);
    }
}
