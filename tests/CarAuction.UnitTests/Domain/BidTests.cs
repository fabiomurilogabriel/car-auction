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
            var auctionId = Guid.NewGuid();
            var bidderId = Guid.NewGuid();

            var bid = new Bid(auctionId, bidderId, 12000m, Region.USEast, 1);

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
            var bid = CreateTestBid();

            bid.Accept();

            Assert.True(bid.IsAccepted);
            Assert.Empty(bid.RejectionReason);
        }

        [Fact]
        public void Reject_ShouldSetIsAcceptedToFalseAndSetReason()
        {
            var bid = CreateTestBid();
            var reason = "Amount too low";

            bid.Reject(reason);

            Assert.False(bid.IsAccepted);
            Assert.Equal(reason, bid.RejectionReason);
        }

        [Fact]
        public void MarkAsDuringPartition_ShouldSetFlagToTrue()
        {
            var bid = CreateTestBid();

            bid.MarkAsDuringPartition();

            Assert.True(bid.IsDuringPartition);
        }

        [Theory]
        [InlineData(Region.USEast)]
        [InlineData(Region.EUWest)]
        public void ShouldSupportBothRegions(Region region)
        {
            var bid = new Bid(Guid.NewGuid(), Guid.NewGuid(), 10000m, region, 1);

            Assert.Equal(region, bid.OriginRegion);
        }

        private static Bid CreateTestBid()
        {
            return new Bid(Guid.NewGuid(), Guid.NewGuid(), 12000m, Region.USEast, 1);
        }
    }
}