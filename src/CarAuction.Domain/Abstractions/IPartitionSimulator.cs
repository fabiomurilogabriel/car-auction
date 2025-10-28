using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Partitions;

namespace CarAuction.Domain.Abstractions
{
    public interface IPartitionSimulator
    {
        Task SimulatePartitionAsync(Region originBidRegion, Region auctionRegion, TimeSpan duration);
        Task<Region> GetCurrentRegionAsync();
        Task HealPartitionAsync();
        bool IsPartitioned { get; }
    }
}
