using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Bids;
using Xunit;

namespace CarAuction.UnitTests.Domain
{
    public class BidTests
    {
        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Arrange
            var auctionId = Guid.NewGuid();
            var bidderId = Guid.NewGuid();

            // Act
            var bid = new Bid(auctionId, bidderId, 12000m, Region.USEast, 1);

            // Assert
            Assert.Equal(auctionId, bid.AuctionId);
            Assert.Equal(bidderId, bid.BidderId);
            Assert.Equal(12000m, bid.Amount);
            Assert.Equal(Region.USEast, bid.OriginRegion);
            Assert.Equal(1, bid.Sequence);
            Assert.False(bid.IsAccepted);
            Assert.False(bid.IsDuringPartition);
        }

        [Fact]
        public void Accept_ShouldSetIsAcceptedToTrue()
        {
            // Arrange
            var bid = CreateTestBid();

            // Act
            bid.Accept();

            // Assert
            Assert.True(bid.IsAccepted);
            Assert.Empty(bid.RejectionReason);
        }

        [Fact]
        public void Reject_ShouldSetIsAcceptedToFalseAndSetReason()
        {
            // Arrange
            var bid = CreateTestBid();
            var reason = "Amount too low";

            // Act
            bid.Reject(reason);

            // Assert
            Assert.False(bid.IsAccepted);
            Assert.Equal(reason, bid.RejectionReason);
        }

        [Fact]
        public void MarkAsDuringPartition_ShouldSetFlagToTrue()
        {
            // Arrange
            var bid = CreateTestBid();

            // Act
            bid.MarkAsDuringPartition();

            // Assert
            Assert.True(bid.IsDuringPartition);
        }

        [Theory]
        [InlineData(Region.USEast)]
        [InlineData(Region.EUWest)]
        public void ShouldSupportBothRegions(Region region)
        {
            // Arrange & Act
            var bid = new Bid(Guid.NewGuid(), Guid.NewGuid(), 10000m, region, 1);

            // Assert
            Assert.Equal(region, bid.OriginRegion);
        }

        private static Bid CreateTestBid()
        {
            return new Bid(Guid.NewGuid(), Guid.NewGuid(), 12000m, Region.USEast, 1);
        }
    }
}