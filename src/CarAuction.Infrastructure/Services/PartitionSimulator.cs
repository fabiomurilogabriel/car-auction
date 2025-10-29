using CarAuction.Domain.Abstractions;
using CarAuction.Domain.Abstractions.Repositories;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Partitions;
using System.Collections.Concurrent;

namespace CarAuction.Infrastructure.Services
{
    public class PartitionSimulator(IPartitionEventRepository repository, Region localRegion = Region.USEast) : IPartitionSimulator
    {
        private readonly IPartitionEventRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        private readonly ConcurrentDictionary<Region, (PartitionEvent Partition, CancellationTokenSource cancellationTokenSource)> _partitions
            = new();

        private Region _localRegion = localRegion;

        public event EventHandler<PartitionEvent>? PartitionStarted;
        public event EventHandler<PartitionEvent>? PartitionHealed;

        public bool IsPartitioned => !_partitions.IsEmpty;

        public async Task SimulatePartitionAsync(Region originRegion, Region auctionRegion, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero) throw new ArgumentException("duration must be positive", nameof(duration));

            try
            {
                var partition = new PartitionEvent(originRegion, auctionRegion);
                partition.StartPartition();

                // Persist partition event
                await _repository.CreateAsync(partition).ConfigureAwait(false);

                // Prepare cancellation token for this partition auto-heal
                var cancellationTokenSource = new CancellationTokenSource();

                // If a partition for this auctionRegion already exists, cancel previous auto-heal and replace it
                _partitions.AddOrUpdate(
                    auctionRegion,
                    (_) => (partition, cancellationTokenSource),
                    (_, existing) =>
                    {
                        try { existing.cancellationTokenSource.Cancel(); } catch { /* ignore */ }
                        return (partition, cancellationTokenSource);
                    });

                // Raise event (non-blocking)
                RaisePartitionStarted(partition);

                // Auto-heal for this specific region
                var token = cancellationTokenSource.Token;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(duration, token).ConfigureAwait(false);
                        // heal only this region
                        await HealPartitionByRegionAsync(auctionRegion).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // expected when cancelled early
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"PartitionSimulator auto-heal error for {auctionRegion}: {ex}");
                    }
                }, token);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"SimulatePartitionAsync error: {ex.Message}");
                throw;
            }
        }

        public Task<Region> GetCurrentRegionAsync()
        {
            return Task.FromResult(_localRegion);
        }

        public Task SetCurrentRegionAsync(Region region)
        {
            _localRegion = region;
            return Task.CompletedTask;
        }

        public async Task HealPartitionAsync()
        {
            // Heal all active partitions
            var regions = _partitions.Keys.ToArray();

            foreach (var region in regions)
            {
                await HealPartitionByRegionAsync(region).ConfigureAwait(false);
            }
        }

        private async Task HealPartitionByRegionAsync(Region auctionRegion)
        {
            if (_partitions.TryRemove(auctionRegion, out var entry))
            {
                try
                {
                    // Cancel pending auto-heal if still running
                    try { entry.cancellationTokenSource.Cancel(); } catch { /* ignore */ }

                    var partition = entry.Partition;
                    partition.Resolve();
                    await _repository.UpdateAsync(partition).ConfigureAwait(false);

                    RaisePartitionHealed(partition);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"HealPartitionByRegionAsync error for {auctionRegion}: {ex.Message}");
                    throw;
                }
            }
        }

        private void RaisePartitionStarted(PartitionEvent partition)
        {
            var handler = PartitionStarted;
            if (handler == null) return;

            foreach (Delegate d in handler.GetInvocationList())
            {
                if (d is EventHandler<PartitionEvent> h)
                {
                    Task.Run(() =>
                    {
                        try { h.Invoke(this, partition); }
                        catch (Exception ex) { Console.Error.WriteLine($"PartitionStarted handler error: {ex.Message}"); }
                    });
                }
            }
        }

        private void RaisePartitionHealed(PartitionEvent partition)
        {
            var handler = PartitionHealed;
            if (handler == null) return;

            foreach (Delegate d in handler.GetInvocationList())
            {
                if (d is EventHandler<PartitionEvent> h)
                {
                    Task.Run(() =>
                    {
                        try { h.Invoke(this, partition); }
                        catch (Exception ex) { Console.Error.WriteLine($"PartitionHealed handler error: {ex.Message}"); }
                    });
                }
            }
        }
    }
}
