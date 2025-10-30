using CarAuction.Domain.Models;
using CarAuction.Infrastructure.Data.Repositories;
using CarAuction.Infrastructure.Services;
using CarAuction.IntegrationTests.Helpers;

namespace CarAuction.IntegrationTests
{
    public class CompletePartitionSimulationTest
    {
        [Fact]
        public async Task CompletePartitionScenario_ShouldHandleCorrectly()
        {
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

            auction = await auctionRepo.GetByIdAsync(auction.Id);
            auction.Start();
            await auctionRepo.UpdateAsync(auction);

            var bid1 = TestDataBuilder.CreateTestBid(auction.Id, 11000m, Region.USEast, 1);
            await bidRepo.CreateAsync(bid1);

            var savedBid1 = await bidRepo.GetByIdAsync(bid1.Id);
            Assert.NotNull(savedBid1);
            Assert.Equal(11000m, savedBid1.Amount);

            await simulator.SimulatePartitionAsync(
                Region.USEast,
                Region.EUWest,
                TimeSpan.FromSeconds(2)
            );

            Assert.True(simulator.IsPartitioned);

            var bid2 = TestDataBuilder.CreateTestBid(auction.Id, 12000m, Region.USEast, 2);
            await bidRepo.CreateAsync(bid2);

            var bid3 = TestDataBuilder.CreateTestBid(auction.Id, 13000m, Region.EUWest, 3);
            bid3.MarkAsDuringPartition();
            await bidRepo.CreateAsync(bid3);

            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.False(simulator.IsPartitioned);

            var allBids = await bidRepo.GetByAuctionIdAsync(auction.Id);

            Assert.Equal(3, allBids.Count());

            var partitionBids = await bidRepo.GetBidsMadeDuringPartitionWithinAuctionDeadlineAsync(auction.Id, auction.EndTime);
            Assert.Single(partitionBids);
            Assert.Equal(13000m, partitionBids.First().Amount);
            Assert.Equal(Region.EUWest, partitionBids.First().OriginRegion);

            var finalAuction = await auctionRepo.GetByIdAsync(auction.Id);
            Assert.NotNull(finalAuction);

            var orderedBids = allBids.OrderBy(b => b.Sequence).ToList();
            Assert.Equal(11000m, orderedBids[0].Amount);
            Assert.Equal(12000m, orderedBids[1].Amount);
            Assert.Equal(13000m, orderedBids[2].Amount);

            Assert.Equal(Region.USEast, orderedBids[0].OriginRegion);
            Assert.Equal(Region.USEast, orderedBids[1].OriginRegion);
            Assert.Equal(Region.EUWest, orderedBids[2].OriginRegion);


        }

        [Fact]
        public async Task PartitionDuringAuctionEnd_ShouldHandleCorrectly()
        {
            var context = TestDbContextFactory.CreateInMemoryContext();
            var auctionRepo = new AuctionRepository(context);
            var bidRepo = new BidRepository(context);
            var partitionRepo = new PartitionEventRepository(context);
            var simulator = new PartitionSimulator(partitionRepo);

            var vehicle = TestDataBuilder.CreateTestVehicle(Region.USEast);
            context.Vehicles.Add(vehicle);
            await context.SaveChangesAsync();

            var auction = TestDataBuilder.CreateTestAuction(
                vehicle.Id, 
                Region.USEast, 
                DateTime.UtcNow.AddSeconds(-10),
                DateTime.UtcNow.AddSeconds(1)
            );
            await auctionRepo.CreateAsync(auction);
            auction.Start();
            await auctionRepo.UpdateAsync(auction);

            var bid1 = TestDataBuilder.CreateTestBid(auction.Id, 15000m, Region.USEast, 1);
            await bidRepo.CreateAsync(bid1);

            await simulator.SimulatePartitionAsync(Region.USEast, Region.EUWest, TimeSpan.FromSeconds(3));

            await Task.Delay(TimeSpan.FromSeconds(2));
            
            var bid2 = TestDataBuilder.CreateTestBid(auction.Id, 16000m, Region.EUWest, 2);
            bid2.MarkAsDuringPartition();
            await bidRepo.CreateAsync(bid2);

            await Task.Delay(TimeSpan.FromSeconds(2));

            var validBids = await bidRepo.GetBidsMadeDuringPartitionWithinAuctionDeadlineAsync(auction.Id, auction.EndTime);
            Assert.Empty(validBids);

            var allBids = await bidRepo.GetByAuctionIdAsync(auction.Id);
            Assert.Equal(2, allBids.Count());
            

        }
    }
}