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
            // Arrange & Act
            var partitionEvent = new PartitionEvent(Region.USEast, Region.EUWest);

            // Assert
            Assert.Equal(Region.USEast, partitionEvent.OriginBidRegion);
            Assert.Equal(Region.EUWest, partitionEvent.AuctionRegion);
            Assert.Equal(PartitionStatus.Healthy, partitionEvent.Status);
            Assert.Null(partitionEvent.EndTime);
        }

        [Fact]
        public void StartPartition_ShouldChangeStatusToPartitioned()
        {
            // Arrange
            var partitionEvent = new PartitionEvent(Region.USEast, Region.EUWest);

            // Act
            partitionEvent.StartPartition();

            // Assert
            Assert.Equal(PartitionStatus.Partitioned, partitionEvent.Status);
        }

        [Fact]
        public void BeginReconciliation_ShouldChangeStatusToReconciling()
        {
            // Arrange
            var partitionEvent = new PartitionEvent(Region.USEast, Region.EUWest);
            partitionEvent.StartPartition();

            // Act
            partitionEvent.BeginReconciliation();

            // Assert
            Assert.Equal(PartitionStatus.Reconciling, partitionEvent.Status);
        }

        [Fact]
        public void Resolve_ShouldChangeStatusToResolvedAndSetEndTime()
        {
            // Arrange
            var partitionEvent = new PartitionEvent(Region.USEast, Region.EUWest);
            partitionEvent.StartPartition();
            partitionEvent.BeginReconciliation();

            // Act
            partitionEvent.Resolve();

            // Assert
            Assert.Equal(PartitionStatus.Resolved, partitionEvent.Status);
            Assert.NotNull(partitionEvent.EndTime);
        }

        [Fact]
        public void ResetToHealthy_ShouldChangeStatusToHealthy()
        {
            // Arrange
            var partitionEvent = new PartitionEvent(Region.USEast, Region.EUWest);
            partitionEvent.StartPartition();

            // Act
            partitionEvent.ResetToHealthy();

            // Assert
            Assert.Equal(PartitionStatus.Healthy, partitionEvent.Status);
        }

        [Fact]
        public void PartitionLifecycle_ShouldFollowCorrectSequence()
        {
            // Arrange
            var partitionEvent = new PartitionEvent(Region.USEast, Region.EUWest);

            // Act & Assert - Healthy -> Partitioned -> Reconciling -> Resolved
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