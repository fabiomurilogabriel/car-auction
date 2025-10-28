using CarAuction.Domain.Abstractions;
using CarAuction.Domain.Abstractions.Repositories;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Partitions;

namespace CarAuction.Infrastructure.Services
{
    public class PartitionSimulator(IPartitionEventRepository repository) : IPartitionSimulator
    {
        private bool _isPartitioned = false;
        private readonly IPartitionEventRepository _repository = repository;
        private PartitionEvent? _currentPartition;

        public bool IsPartitioned => _isPartitioned;

        public async Task SimulatePartitionAsync(Region originRegion, Region auctionRegion, TimeSpan createdAt)
        {
            _currentPartition = new PartitionEvent(originRegion, auctionRegion);
            _currentPartition.StartPartition();

            await _repository.AddAsync(_currentPartition);

            _isPartitioned = true;

            // Auto-heal after duration
            _ = Task.Run(async () =>
            {
                await Task.Delay(createdAt);
                await HealPartitionAsync();
            });
        }

        public async Task<Region> GetCurrentRegionAsync()
        {
            if (_currentPartition != null)
            {
                return await Task.FromResult(_currentPartition.AuctionRegion);
            }

            // Se não houver partição, retorna a região padrão
            return await Task.FromResult(Region.EUWest);
        }

        public async Task HealPartitionAsync()
        {
            if (_currentPartition != null)
            {
                _currentPartition.Resolve();
                await _repository.UpdateAsync(_currentPartition);
            }

            _isPartitioned = false;
        }
    }
}
