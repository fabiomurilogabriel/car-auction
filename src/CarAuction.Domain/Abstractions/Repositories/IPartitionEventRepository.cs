using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Partitions;

namespace CarAuction.Domain.Abstractions.Repositories
{
    public interface IPartitionEventRepository
    {
        Task<PartitionEvent> GetCurrentPartitionAsync();
        Task<PartitionEvent> GetCurrentPartitionByAuctionRegionAsync(Region auctionRegion);
        Task<Guid> AddAsync(PartitionEvent partitionEvent);
        Task UpdateAsync(PartitionEvent partitionEvent);
        Task<IEnumerable<PartitionEvent>> GetHistoryAsync(DateTime since);
    }
}
