using CarAuction.Application.Services;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Bids;

namespace CarAuction.UnitTests.Services
{
    public class ConflictResolverTests
    {
        private readonly ConflictResolver _conflictResolver;

        public ConflictResolverTests()
        {
            _conflictResolver = new ConflictResolver();
        }

        [Fact]
        public async Task ResolveConflictingBidsAsync_WithPartitionBidsFromSameRegion_ShouldAcceptFirstBySequence()
        {
            var auctionId = Guid.NewGuid();
            
            var bid1 = new Bid(auctionId, Guid.NewGuid(), 10000m, Region.USEast, 1);
            await Task.Delay(1);
            var bid2 = new Bid(auctionId, Guid.NewGuid(), 11000m, Region.USEast, 2);
            await Task.Delay(1);
            var bid3 = new Bid(auctionId, Guid.NewGuid(), 12000m, Region.USEast, 3);
            
            bid1.MarkAsDuringPartition();
            bid2.MarkAsDuringPartition();
            bid3.MarkAsDuringPartition();
            
            var allBids = new List<Bid> { bid3, bid1, bid2 };

            var resolvedBids = await _conflictResolver.ResolveConflictingBidsAsync(allBids, Region.USEast);

            var resolvedList = resolvedBids.ToList();
            Assert.Equal(3, resolvedList.Count);
            
            var acceptedBid = resolvedList.First(b => b.IsAccepted);
            var rejectedBids = resolvedList.Where(b => !b.IsAccepted).ToList();
            
            Assert.Equal(12000m, acceptedBid.Amount);
            Assert.Equal(2, rejectedBids.Count);
            Assert.All(rejectedBids, b => Assert.Contains("Lost in conflict resolution", b.RejectionReason));
        }

        [Fact]
        public async Task DetermineFinalWinnerAsync_WithMultipleAcceptedBids_ShouldReturnHighestAmount()
        {
            var auctionId = Guid.NewGuid();
            
            var bid1 = new Bid(auctionId, Guid.NewGuid(), 10000m, Region.USEast, 1);
            var bid2 = new Bid(auctionId, Guid.NewGuid(), 15000m, Region.EUWest, 2);
            var bid3 = new Bid(auctionId, Guid.NewGuid(), 12000m, Region.USEast, 3);
            
            bid1.Accept();
            bid2.Accept();
            bid3.Accept();
            
            var allBids = new List<Bid> { bid1, bid2, bid3 };

            var winner = await _conflictResolver.DetermineFinalWinnerAsync(allBids);

            Assert.NotNull(winner);
            Assert.Equal(15000m, winner.Amount);
            Assert.Equal(Region.EUWest, winner.OriginRegion);
            Assert.True(winner.IsAccepted);
            Assert.False(bid1.IsAccepted);
            Assert.False(bid3.IsAccepted);
        }

        [Fact]
        public async Task DetermineFinalWinnerAsync_WithNoAcceptedBids_ShouldReturnNull()
        {
            var auctionId = Guid.NewGuid();
            var bid1 = new Bid(auctionId, Guid.NewGuid(), 10000m, Region.USEast, 1);
            var bid2 = new Bid(auctionId, Guid.NewGuid(), 11000m, Region.EUWest, 2);
            
            var allBids = new List<Bid> { bid1, bid2 };

            var winner = await _conflictResolver.DetermineFinalWinnerAsync(allBids);

            Assert.Null(winner);
        }

        [Fact]
        public async Task DetermineFinalWinnerAsync_WithHigherRejectedBid_ShouldIgnoreRejectedBids()
        {
            var auctionId = Guid.NewGuid();
            
            var bid1 = new Bid(auctionId, Guid.NewGuid(), 10000m, Region.USEast, 1);
            var bid2 = new Bid(auctionId, Guid.NewGuid(), 20000m, Region.EUWest, 2);
            var bid3 = new Bid(auctionId, Guid.NewGuid(), 12000m, Region.USEast, 3);
            
            bid1.Accept();
            bid2.Reject("Invalid bid");
            bid3.Accept();
            
            var allBids = new List<Bid> { bid1, bid2, bid3 };

            var winner = await _conflictResolver.DetermineFinalWinnerAsync(allBids);

            Assert.NotNull(winner);
            Assert.Equal(12000m, winner.Amount);
            Assert.Equal(bid3.Id, winner.Id);
            Assert.True(winner.IsAccepted);
            Assert.False(bid1.IsAccepted);
            Assert.False(bid2.IsAccepted);
        }


        [Fact]
        public async Task DetermineFinalWinnerAsync_OnlyAcceptedBidsCompete_ShouldIgnoreRejected()
        {
            var auctionId = Guid.NewGuid();
            
            var bid1 = new Bid(auctionId, Guid.NewGuid(), 15000m, Region.USEast, 1);
            var bid2 = new Bid(auctionId, Guid.NewGuid(), 8000m, Region.EUWest, 2);
            
            bid1.Reject("Too high");
            bid2.Accept();
            
            var allBids = new List<Bid> { bid1, bid2 };

            var winner = await _conflictResolver.DetermineFinalWinnerAsync(allBids);

            Assert.NotNull(winner);
            Assert.Equal(8000m, winner.Amount);
            Assert.Equal(bid2.Id, winner.Id);
            Assert.True(winner.IsAccepted);
        }
    }
}