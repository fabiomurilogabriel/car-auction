using CarAuction.Domain.Models;
using CarAuction.Infrastructure.Data.Repositories;
using CarAuction.Infrastructure.Services;
using CarAuction.IntegrationTests.Helpers;
using Xunit;

namespace CarAuction.IntegrationTests
{
    public class PartitionScenarioTests
    {
        [Fact]
        public async Task CrossRegionBidding_ShouldWorkNormally()
        {
            // Arrange
            var context = TestDbContextFactory.CreateInMemoryContext();
            var auctionRepo = new AuctionRepository(context);
            var bidRepo = new BidRepository(context);

            var vehicle = TestDataBuilder.CreateTestVehicle(Region.USEast);
            context.Vehicles.Add(vehicle);
            await context.SaveChangesAsync();

            var auction = TestDataBuilder.CreateTestAuction(vehicle.Id, Region.USEast);
            await auctionRepo.CreateAsync(auction);
            auction.Start();
            await auctionRepo.UpdateAsync(auction);

            // Act - Bid from different region
            var euBid = TestDataBuilder.CreateTestBid(
                auction.Id, 11000m, Region.EUWest, 1);
            await bidRepo.CreateAsync(euBid);

            // Assert
            var savedBid = await bidRepo.GetByIdAsync(euBid.Id);
            Assert.NotNull(savedBid);
            Assert.Equal(Region.EUWest, savedBid.OriginRegion);
        }

        [Fact]
        public async Task BiddingDuringPartition_ShouldMarkBidsCorrectly()
        {
            // Arrange
            var context = TestDbContextFactory.CreateInMemoryContext();
            var auctionRepo = new AuctionRepository(context);
            var bidRepo = new BidRepository(context);
            var partitionRepo = new PartitionEventRepository(context);
            var simulator = new PartitionSimulator(partitionRepo);

            var vehicle = TestDataBuilder.CreateTestVehicle(Region.USEast);
            context.Vehicles.Add(vehicle);
            await context.SaveChangesAsync();

            var auction = TestDataBuilder.CreateTestAuction(vehicle.Id, Region.USEast);
            await auctionRepo.CreateAsync(auction);
            auction.Start();
            await auctionRepo.UpdateAsync(auction);

            // Act - Start partition
            await simulator.SimulatePartitionAsync(
                Region.USEast, Region.EUWest, TimeSpan.FromSeconds(5));

            var bid = TestDataBuilder.CreateTestBid(auction.Id, 11000m, Region.EUWest, 1);
            bid.MarkAsDuringPartition();
            await bidRepo.CreateAsync(bid);

            // Assert
            var partitionBids = await bidRepo.GetBidsMadeDuringPartitionWithinAuctionDeadlineAsync(auction.Id, auction.EndTime);
            Assert.Single(partitionBids);
            Assert.True(partitionBids.First().IsDuringPartition);
        }
    }
}
