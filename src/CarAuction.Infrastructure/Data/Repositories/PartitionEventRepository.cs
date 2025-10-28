using CarAuction.Domain.Abstractions.Repositories;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Partitions;
using Microsoft.EntityFrameworkCore;

namespace CarAuction.Infrastructure.Data.Repositories
{
    public class PartitionEventRepository(AuctionDbContext context) : IPartitionEventRepository
    {
        private readonly AuctionDbContext _context = context;

        public async Task<PartitionEvent> GetCurrentPartitionAsync()
            => await _context.PartitionEvents
                .Where(p => p.Status == PartitionStatus.Partitioned 
                    || p.Status == PartitionStatus.Reconciling)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

        public async Task<PartitionEvent> GetCurrentPartitionByAuctionRegionAsync(Region auctionRegion)
            => await _context.PartitionEvents
                .Where(p => 
                    (p.Status == PartitionStatus.Partitioned
                    || p.Status == PartitionStatus.Reconciling)
                    && p.AuctionRegion == auctionRegion)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

        public async Task<Guid> AddAsync(PartitionEvent partitionEvent)
        {
            _context.PartitionEvents.Add(partitionEvent);
            await _context.SaveChangesAsync();
            return partitionEvent.Id;
        }

        public async Task UpdateAsync(PartitionEvent partitionEvent)
        {
            _context.PartitionEvents.Update(partitionEvent);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<PartitionEvent>> GetHistoryAsync(DateTime since)
            => await _context.PartitionEvents
                .Where(p => p.CreatedAt >= since)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
    }
}
