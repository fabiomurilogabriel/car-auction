using CarAuction.Domain.Abstractions.Repositories;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Auctions;
using Microsoft.EntityFrameworkCore;

namespace CarAuction.Infrastructure.Data.Repositories
{
    public class AuctionRepository(AuctionDbContext context) : IAuctionRepository
    {
        private readonly AuctionDbContext _context = context;

        // cenario consistente eventual - AP
        public async Task<Auction> GetByIdAsync(Guid id)
            => await _context.Auctions
                .Include(a => a.Vehicle)
                .FirstOrDefaultAsync(a => a.Id == id);

        public async Task<IEnumerable<Auction>> GetActiveAuctionsByRegionAsync(Region region)
            => await _context.Auctions
                .Where(a => a.Region == region && a.State == AuctionState.Active)
                .ToListAsync();

        public async Task<Guid> CreateAsync(Auction auction)
        {
            _context.Auctions.Add(auction);

            // cria uma entrada inicial na tabela BidSequences para este leilão
            _context.BidSequences.Add(new BidSequence
            {
                AuctionId = auction.Id,
                CurrentSequence = 0,
                LastUpdated = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return auction.Id;
        }

        public async Task<bool> UpdateAsync(Auction auction)
        {
            try
            {
                _context.Auctions.Update(auction);

                await _context.SaveChangesAsync();
                
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }

        // cenario consistecia forte - CP
        public async Task<Auction> GetWithBidsAsync(Guid id)
        {
            var auction = await _context.Auctions
                .Include(a => a.Vehicle)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (auction is null)
                return null;

            var bids = await _context.Bids
                .Where(b => b.AuctionId == id)
                .OrderBy(b => b.Sequence)
                .ToListAsync();

            auction.ClearBids();
            
            foreach (var bid in bids)
            {
                auction.AddBid(bid);
            }

            return auction;
        }

        public async Task<IEnumerable<Auction>> GetAuctionsThatNeedsReconciliationByRegionAsync(Region region)
            => await _context.Auctions
                .Where(a =>
                    a.Region == region &&
                    a.State == AuctionState.Paused)
                .ToListAsync();
        
    }
}
