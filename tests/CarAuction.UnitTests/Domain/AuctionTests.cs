using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Auctions;
using CarAuction.Domain.Models.Bids;
using Xunit;

namespace CarAuction.UnitTests.Domain
{
    public class AuctionTests
    {
        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Arrange
            var vehicleId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;
            var endTime = startTime.AddHours(1);

            // Act
            var auction = new Auction(vehicleId, Region.USEast, 10000m, 15000m, startTime, endTime);

            // Assert
            Assert.Equal(vehicleId, auction.VehicleId);
            Assert.Equal(Region.USEast, auction.Region);
            Assert.Equal(10000m, auction.StartingPrice);
            Assert.Equal(15000m, auction.ReservePrice);
            Assert.Equal(10000m, auction.CurrentPrice);
            Assert.Equal(AuctionState.Draft, auction.State);
        }

        [Fact]
        public void Start_ShouldChangeStateToActive()
        {
            // Arrange
            var auction = CreateTestAuction();

            // Act
            auction.Start();

            // Assert
            Assert.Equal(AuctionState.Active, auction.State);
        }

        [Fact]
        public void Pause_ShouldChangeStateToPaused()
        {
            // Arrange
            var auction = CreateTestAuction();
            auction.Start();

            // Act
            auction.Pause();

            // Assert
            Assert.Equal(AuctionState.Paused, auction.State);
        }

        [Fact]
        public void End_ShouldChangeStateToEnded()
        {
            // Arrange
            var auction = CreateTestAuction();
            auction.Start();

            // Act
            auction.End();

            // Assert
            Assert.Equal(AuctionState.Ended, auction.State);
        }

        [Fact]
        public void TryPlaceBid_WithHigherAmount_ShouldAcceptAndUpdatePrice()
        {
            // Arrange
            var auction = CreateTestAuction();
            auction.Start();
            var bid = new Bid(auction.Id, Guid.NewGuid(), 12000m, Region.USEast, 1);

            // Act
            var result = auction.TryPlaceBid(bid);

            // Assert
            Assert.True(result);
            Assert.Equal(12000m, auction.CurrentPrice);
        }

        [Fact]
        public void TryPlaceBid_WithLowerAmount_ShouldReject()
        {
            // Arrange
            var auction = CreateTestAuction();
            auction.Start();
            var bid = new Bid(auction.Id, Guid.NewGuid(), 9000m, Region.USEast, 1);

            // Act
            var result = auction.TryPlaceBid(bid);

            // Assert
            Assert.False(result);
            Assert.Equal(10000m, auction.CurrentPrice);
        }

        [Fact]
        public void UpdateWinningBid_ShouldSetWinnerAndPrice()
        {
            // Arrange
            var auction = CreateTestAuction();
            var bidderId = Guid.NewGuid();

            // Act
            auction.UpdateWinningBid(15000m, bidderId);

            // Assert
            Assert.Equal(15000m, auction.CurrentPrice);
            Assert.Equal(bidderId, auction.WinningBidderId);
        }

        private static Auction CreateTestAuction()
        {
            return new Auction(
                Guid.NewGuid(),
                Region.USEast,
                10000m,
                15000m,
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(1)
            );
        }
    }
}