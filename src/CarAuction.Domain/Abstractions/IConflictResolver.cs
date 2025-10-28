using CarAuction.Domain.Abstractions.Results;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Bids;

namespace CarAuction.Domain.Abstractions
{
    public interface IConflictResolver
    {
        Task ResolveConflictingBidsAsync(List<Bid> allBids, Region region);
        Task<Bid> DetermineFinalWinnerAsync(List<Bid> allBids);
    }
}
