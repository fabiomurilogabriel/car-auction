using CarAuction.Domain.Models.Bids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarAuction.Domain.Abstractions.Repositories
{
    public interface IBidRepository
    {
        Task<Bid> GetByIdAsync(Guid id);
        Task<IEnumerable<Bid>> GetByAuctionIdAsync(Guid auctionId);
        Task<long> GetNextSequenceAsync(Guid auctionId);
        Task<Guid> CreateAsync(Bid bid);
        Task<IEnumerable<Bid>> GetBidsMadeDuringPartitionWithinAuctionDeadlineAsync(Guid auctionId, DateTime auctionEndDate);
        Task UpdateAsync(Bid bid);
        Task UpdateRangeAsync(IEnumerable<Bid> bids);
    }
}
