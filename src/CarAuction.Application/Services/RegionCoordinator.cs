using CarAuction.Domain.Abstractions;
using CarAuction.Domain.Abstractions.Repositories;
using CarAuction.Domain.Abstractions.Results;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Partitions;

namespace CarAuction.Application.Services
{
    public class RegionCoordinator(
        IPartitionEventRepository partitionRepository,
        IPartitionSimulator partitionSimulator) : IRegionCoordinator
    {
        private readonly IPartitionEventRepository _partitionRepository = partitionRepository;
        private readonly IPartitionSimulator _partitionSimulator = partitionSimulator;

        public event EventHandler<PartitionEventArgs> PartitionDetected;
        public event EventHandler<PartitionEventArgs> PartitionHealed;

        private PartitionStatus _lastStatus = PartitionStatus.Healthy;

        public async Task<bool> IsRegionReachableAsync(Region region)
        {
            try
            {
                if (_partitionSimulator.IsPartitioned)
                {
                    return false;
                }

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in IsRegionReachableAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<T> ExecuteInRegionAsync<T>(Region region, Func<Task<T>> operation)
        {
            try
            {
                var isReachable = await IsRegionReachableAsync(region);

                if (!isReachable)
                {
                    throw new InvalidOperationException($"Region {region} is not reachable");
                }

                return await operation();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in ExecuteInRegionAsync: {ex.Message}");
                throw;
            }            
        }

        public async Task<PartitionStatus> GetPartitionStatusAsync()
        {
            try
            {
                PartitionStatus currentStatus;

                if (_partitionSimulator.IsPartitioned)
                {
                    currentStatus = PartitionStatus.Partitioned;
                }
                else
                {
                    var currentPartition = await _partitionRepository.GetCurrentPartitionAsync();
                    currentStatus = currentPartition?.Status ?? PartitionStatus.Healthy;
                }

                if (currentStatus != _lastStatus)
                {
                    // Obtém as regiões envolvidas da partição atual, ou define padrões
                    var currentPartition = await _partitionRepository.GetCurrentPartitionAsync();

                    Region originRegion = currentPartition.OriginBidRegion;
                    Region auctionRegion = currentPartition.AuctionRegion;

                    if (currentStatus == PartitionStatus.Partitioned)
                    {
                        await AddPartitionAsync(originRegion, auctionRegion);
                    }
                    else if (_lastStatus == PartitionStatus.Partitioned && currentStatus == PartitionStatus.Healthy)
                    {
                        // Atualiza entidade para resolvida
                        if (currentPartition != null)
                        {
                            currentPartition.Resolve();
                            await _partitionRepository.UpdateAsync(currentPartition);
                        }

                        var args = new PartitionEventArgs
                        {
                            Status = currentStatus,
                            CreatedAt = currentPartition?.CreatedAt ?? DateTime.UtcNow,
                            OriginBidRegion = originRegion,
                            AuctionRegion = auctionRegion,
                            UpdatedAt = currentPartition?.EndTime,
                            IsSolved = true
                        };

                        OnPartitionHealed(args);
                    }

                    _lastStatus = currentStatus;
                }

                return currentStatus;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in GetPartitionStatusAsync: {ex.Message}");
                throw;
            }
            
        }

        public async Task AddPartitionAsync(Region originBidRegion, Region auctionRegion)
        {
            try
            {
                var partition = await _partitionRepository.GetCurrentPartitionByAuctionRegionAsync(auctionRegion);

                if (partition is null)
                {
                    var partitionEvent = new PartitionEvent(originBidRegion, auctionRegion);
                    partitionEvent.StartPartition();

                    await _partitionRepository.AddAsync(partitionEvent);

                    var args = new PartitionEventArgs
                    {
                        Status = partitionEvent.Status,
                        CreatedAt = partitionEvent.CreatedAt,
                        OriginBidRegion = partitionEvent.OriginBidRegion,
                        AuctionRegion = partitionEvent.AuctionRegion,
                        IsSolved = false
                    };

                    OnPartitionDetected(args);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in AddPartitionAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<PartitionEvent> GetCurrentPartitionByAuctionRegionAsync(Region auctionRegion)
        {
            try
            {
                return await _partitionRepository.GetCurrentPartitionByAuctionRegionAsync(auctionRegion);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in GetCurrentPartitionByAuctionRegionAsync: {ex.Message}");
                throw;
            }

        }

        public async Task<PartitionEvent> UpdatePartitionByAuctionRegionAsync(Region auctionRegion, PartitionStatus partitionStatus)
        {
            try
            {
                var partition = await _partitionRepository.GetCurrentPartitionByAuctionRegionAsync(auctionRegion);

                if (partition is null)
                {
                    throw new Exception($"Partition by Region: {auctionRegion} not found");
                }

                switch (partitionStatus)
                {
                    case PartitionStatus.Healthy:
                        partition.ResetToHealthy();
                        break;
                    case PartitionStatus.Partitioned:
                        partition.StartPartition();
                        break;
                    case PartitionStatus.Reconciling:
                        partition.BeginReconciliation();
                        break;
                    case PartitionStatus.Resolved:
                        partition.Resolve();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(partitionStatus), $"Invalid partition status: {partitionStatus}");
                }

                await _partitionRepository.UpdateAsync(partition);

                return partition;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in UpdatePartitionByAuctionRegionAsync: {ex.Message}");
                throw;
            }
        }

        protected virtual void OnPartitionDetected(PartitionEventArgs e)
        {
            PartitionDetected?.Invoke(this, e);
        }

        protected virtual void OnPartitionHealed(PartitionEventArgs e)
        {
            PartitionHealed?.Invoke(this, e);
        }
    }
}
