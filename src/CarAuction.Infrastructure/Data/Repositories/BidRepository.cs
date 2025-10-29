using CarAuction.Domain.Abstractions.Repositories;
using CarAuction.Domain.Models.Bids;
using Microsoft.EntityFrameworkCore;

namespace CarAuction.Infrastructure.Data.Repositories
{
    public class BidRepository(AuctionDbContext context) : IBidRepository
    {
        private readonly AuctionDbContext _context = context;

        public async Task<Bid> GetByIdAsync(Guid id)
            => await _context.Bids
                .Include(b => b.Auction)
                .FirstOrDefaultAsync(b => b.Id == id);

        public async Task<IEnumerable<Bid>> GetByAuctionIdAsync(Guid auctionId)
            => await _context.Bids
                .Where(b => b.AuctionId == auctionId)
                .OrderBy(b => b.Sequence)
                .ToListAsync();

        public async Task<long> GetNextSequenceAsync(Guid auctionId)
        {
            var bidSequence = await _context.BidSequences.FindAsync(auctionId);

            if (bidSequence is null)
            {
                bidSequence = new BidSequence
                {
                    AuctionId = auctionId,
                    CurrentSequence = 0,
                    LastUpdated = DateTime.UtcNow
                };
                _context.BidSequences.Add(bidSequence);
            }

            bidSequence.CurrentSequence++;
            bidSequence.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return bidSequence.CurrentSequence;
        }

        public async Task<Guid> CreateAsync(Bid bid)
        {
            _context.Bids.Add(bid);
            await _context.SaveChangesAsync();
            return bid.Id;
        }

        public async Task<IEnumerable<Bid>> GetBidsMadeDuringPartitionWithinAuctionDeadlineAsync(Guid auctionId, DateTime auctionEndDate)
            => await _context.Bids
                .Where(b => b.AuctionId == auctionId && b.IsDuringPartition && b.CreatedAt < auctionEndDate)
                .OrderBy(b => b.CreatedAt)
                .ToListAsync();

        public async Task UpdateAsync(Bid bid)
        {
            try
            {
                _context.Bids.Update(bid);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to update bid {bid.Id}: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateRangeAsync(IEnumerable<Bid> bids)
        {
            try
            {
                _context.Bids.UpdateRange(bids);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to update bids: {ex.Message}");
                throw;
            }
        }

    }
}
