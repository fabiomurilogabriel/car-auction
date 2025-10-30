using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Partitions;
using Xunit;

namespace CarAuction.UnitTests.Domain
{
    public class PartitionEventTests
    {
        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            var partitionEvent = new PartitionEvent(Region.USEast, Region.EUWest);

            Assert.Equal(Region.USEast, partitionEvent.OriginBidRegion);
            Assert.Equal(Region.EUWest, partitionEvent.AuctionRegion);
            Assert.Equal(PartitionStatus.Healthy, partitionEvent.Status);
            Assert.Null(partitionEvent.EndTime);
        }

        [Fact]
        public void StartPartition_ShouldChangeStatusToPartitioned()
        {
            var partitionEvent = new PartitionEvent(Region.USEast, Region.EUWest);

            partitionEvent.StartPartition();

            Assert.Equal(PartitionStatus.Partitioned, partitionEvent.Status);
        }

        [Fact]
        public void BeginReconciliation_ShouldChangeStatusToReconciling()
        {
            var partitionEvent = new PartitionEvent(Region.USEast, Region.EUWest);
            partitionEvent.StartPartition();

            partitionEvent.BeginReconciliation();

            Assert.Equal(PartitionStatus.Reconciling, partitionEvent.Status);
        }

        [Fact]
        public void Resolve_ShouldChangeStatusToResolvedAndSetEndTime()
        {
            var partitionEvent = new PartitionEvent(Region.USEast, Region.EUWest);
            partitionEvent.StartPartition();
            partitionEvent.BeginReconciliation();

            partitionEvent.Resolve();

            Assert.Equal(PartitionStatus.Resolved, partitionEvent.Status);
            Assert.NotNull(partitionEvent.EndTime);
        }

        [Fact]
        public void ResetToHealthy_ShouldChangeStatusToHealthy()
        {
            var partitionEvent = new PartitionEvent(Region.USEast, Region.EUWest);
            partitionEvent.StartPartition();

            partitionEvent.ResetToHealthy();

            Assert.Equal(PartitionStatus.Healthy, partitionEvent.Status);
        }

        [Fact]
        public void PartitionLifecycle_ShouldFollowCorrectSequence()
        {
            var partitionEvent = new PartitionEvent(Region.USEast, Region.EUWest);

            Assert.Equal(PartitionStatus.Healthy, partitionEvent.Status);

            partitionEvent.StartPartition();
            Assert.Equal(PartitionStatus.Partitioned, partitionEvent.Status);

            partitionEvent.BeginReconciliation();
            Assert.Equal(PartitionStatus.Reconciling, partitionEvent.Status);

            partitionEvent.Resolve();
            Assert.Equal(PartitionStatus.Resolved, partitionEvent.Status);
            Assert.NotNull(partitionEvent.EndTime);
        }
    }
}