using CarAuction.Domain.Abstractions.Results;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Partitions;

namespace CarAuction.Domain.Abstractions
{
    public interface IRegionCoordinator
    {
        Task<bool> IsRegionReachableAsync(Region region);
        Task<T> ExecuteInRegionAsync<T>(Region region, Func<Task<T>> operation);
        Task<PartitionStatus> GetPartitionStatusAsync();

        // tive que adicionar esse no contrato para adicionar uma particao no banco de testes
        Task AddPartitionAsync(Region originBidRegion, Region auctionRegion);

        Task<PartitionEvent> GetCurrentPartitionByAuctionRegionAsync(Region auctionRegion);

        Task<PartitionEvent> UpdatePartitionByAuctionRegionAsync(Region auctionRegion, PartitionStatus partitionStatus);

        event EventHandler<PartitionEventArgs> PartitionDetected;
        event EventHandler<PartitionEventArgs> PartitionHealed;
    }
}
