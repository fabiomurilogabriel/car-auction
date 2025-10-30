using CarAuction.Application.Services;
using CarAuction.Domain.Abstractions.Requests;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Auctions;
using CarAuction.Infrastructure.Data.Repositories;
using CarAuction.Infrastructure.Services;
using CarAuction.IntegrationTests.Helpers;

namespace CarAuction.IntegrationTests
{
    public class ExactChallengeScenarioTest
    {
        [Fact]
        public async Task ExactChallengeScenario_5MinutePartition_ShouldMeetAllRequirements()
        {

            var context = TestDbContextFactory.CreateInMemoryContext();
            var auctionRepo = new AuctionRepository(context);
            var bidRepo = new BidRepository(context);
            var partitionRepo = new PartitionEventRepository(context);
            var vehicleRepo = new VehicleRepository(context);
            
            var simulator = new PartitionSimulator(partitionRepo);
            var regionCoordinator = new RegionCoordinator(partitionRepo, simulator);
            var bidOrderingService = new BidOrderingService(bidRepo, auctionRepo);
            var conflictResolver = new ConflictResolver();
            
            var auctionService = new AuctionService(
                auctionRepo, bidRepo, bidOrderingService, 
                regionCoordinator, conflictResolver, simulator);


            var vehicle = TestDataBuilder.CreateTestVehicle(Region.USEast);
            await vehicleRepo.CreateAsync(vehicle);

            var auctionEndTime = DateTime.UtcNow.AddSeconds(3);
            var createRequest = new CreateAuctionRequest
            {
                VehicleId = vehicle.Id,
                Region = Region.USEast,
                StartingPrice = 10000m,
                ReservePrice = 15000m,
                StartTime = DateTime.UtcNow,
                EndTime = auctionEndTime
            };

            var auction = await auctionService.CreateAuctionAsync(createRequest);
            Assert.NotNull(auction);
            Assert.Equal(auction.VehicleId, createRequest.VehicleId);
            Assert.Equal(auction.Region, createRequest.Region);
            Assert.Equal(auction.StartingPrice, createRequest.StartingPrice);
            Assert.Equal(auction.ReservePrice, createRequest.ReservePrice);
            Assert.Equal(auction.StartTime, createRequest.StartTime);
            Assert.Equal(auction.EndTime, createRequest.EndTime);


            await simulator.SetCurrentRegionAsync(Region.USEast);
            var initialBidRequest = new BidRequest
            {
                BidderId = Guid.NewGuid(),
                Amount = 11000m
            };

            var initialBid = await auctionService.PlaceBidAsync(auction.Id, initialBidRequest);
            Assert.True(initialBid.Success);
            Assert.Equal("Bid placed successfully", initialBid.Message);


            await simulator.SimulatePartitionAsync(Region.EUWest, Region.USEast, TimeSpan.FromSeconds(5));
            
            Assert.True(simulator.IsPartitioned);


            await simulator.SetCurrentRegionAsync(Region.USEast);
            var usBidRequest = new BidRequest
            {
                BidderId = Guid.NewGuid(),
                Amount = 12000m
            };

            var usBidResult = await auctionService.PlaceBidAsync(auction.Id, usBidRequest);
            Assert.False(usBidResult.Success);
            Assert.Equal("Auction region is partitioned. Cannot place bid at this time.", usBidResult.Message);


            await simulator.SetCurrentRegionAsync(Region.EUWest);
            var euBidRequest = new BidRequest
            {
                BidderId = Guid.NewGuid(),
                Amount = 13000m
            };

            var euBidResult = await auctionService.PlaceBidAsync(auction.Id, euBidRequest);
            Assert.True(euBidResult.Success);
            Assert.Equal("Bid queued for reconciliation after partition heals", euBidResult.Message);

            await Task.Delay(TimeSpan.FromSeconds(4));

            await Task.Delay(TimeSpan.FromSeconds(2));
            Assert.False(simulator.IsPartitioned);


            var reconciliationResult = await auctionService.ReconcileAuctionAsync(auction.Id);


            var allBids = await bidRepo.GetByAuctionIdAsync(auction.Id);
            var bidsList = allBids.ToList();


            Assert.True(bidsList.Count >= 2, "Lances enfileirados para reconciliação devem ser preservados");
            

            var partitionBids = bidsList.Where(b => b.IsDuringPartition).ToList();
            Assert.True(partitionBids.Any(), "Lances cross-region durante partição devem ser identificados");
            

            Assert.True(reconciliationResult.Success, "Reconciliação deve ser bem-sucedida");
            

            var finalAuction = await auctionService.GetAuctionAsync(auction.Id, ConsistencyLevel.Strong);
            Assert.NotNull(finalAuction);
            Assert.True(finalAuction.State == AuctionState.Ended || finalAuction.State == AuctionState.Active);


            Assert.False(usBidResult.Success, "Lance local durante partição deve ser rejeitado (CP)");
            Assert.Contains("partitioned", usBidResult.Message, StringComparison.OrdinalIgnoreCase);
            

            Assert.True(euBidResult.Success, "Lance cross-region durante partição deve ser enfileirado (AP)");
            Assert.Contains("reconciliation", euBidResult.Message, StringComparison.OrdinalIgnoreCase);


            if (reconciliationResult.WinnerId.HasValue)
            {
                Assert.True(reconciliationResult.Price > 0, "Preço vencedor deve ser válido");
            }


            var validBids = await bidRepo.GetBidsMadeDuringPartitionWithinAuctionDeadlineAsync(auction.Id, auctionEndTime);
        }

        [Fact]
        public async Task PartitionDuringAuctionEnd_ShouldPauseAndReconcile()
        {

            var context = TestDbContextFactory.CreateInMemoryContext($"PartitionDuringAuctionEnd_{Guid.NewGuid()}");
            var auctionRepo = new AuctionRepository(context);
            var bidRepo = new BidRepository(context);
            var partitionRepo = new PartitionEventRepository(context);
            var vehicleRepo = new VehicleRepository(context);
            
            var simulator = new PartitionSimulator(partitionRepo);
            var regionCoordinator = new RegionCoordinator(partitionRepo, simulator);
            var bidOrderingService = new BidOrderingService(bidRepo, auctionRepo);
            var conflictResolver = new ConflictResolver();
            
            var auctionService = new AuctionService(
                auctionRepo, bidRepo, bidOrderingService, 
                regionCoordinator, conflictResolver, simulator);

            var vehicle = TestDataBuilder.CreateTestVehicle(Region.USEast);
            await vehicleRepo.CreateAsync(vehicle);


            var auctionEndTime = DateTime.UtcNow.AddSeconds(30);
            var createRequest = new CreateAuctionRequest
            {
                VehicleId = vehicle.Id,
                Region = Region.EUWest,
                StartingPrice = 10000m,
                ReservePrice = 15000m,
                StartTime = DateTime.UtcNow,
                EndTime = auctionEndTime
            };

            var auction = await auctionService.CreateAuctionAsync(createRequest);


            await simulator.SetCurrentRegionAsync(Region.EUWest);
            var preBid = new BidRequest { BidderId = Guid.NewGuid(), Amount = 11000m };
            var preBidResult = await auctionService.PlaceBidAsync(auction.Id, preBid);
            Assert.True(preBidResult.Success);
            Assert.Equal("Bid placed successfully", preBidResult.Message);

            await simulator.SetCurrentRegionAsync(Region.USEast);
            var secondPreBid = new BidRequest { BidderId = Guid.NewGuid(), Amount = 14000m };
            var secondPreBidResult = await auctionService.PlaceBidAsync(auction.Id, secondPreBid);
            Assert.True(secondPreBidResult.Success);
            Assert.Equal("Bid placed successfully", secondPreBidResult.Message);


            await simulator.SimulatePartitionAsync(Region.USEast, Region.EUWest, TimeSpan.FromSeconds(5));
            
            await simulator.SetCurrentRegionAsync(Region.USEast);
            var partitionBid = new BidRequest { BidderId = Guid.NewGuid(), Amount = 15000m };
            var partitionBidResult = await auctionService.PlaceBidAsync(auction.Id, partitionBid);
            Assert.True(partitionBidResult.Success);
            Assert.Equal("Bid queued for reconciliation after partition heals", partitionBidResult.Message);


            await Task.Delay(TimeSpan.FromSeconds(3));


            var reconciliation = await auctionService.ReconcileAuctionAsync(auction.Id);
            Assert.True(reconciliation.Success);
            Assert.Equal(reconciliation.WinnerId, partitionBidResult.Bid.BidderId);
            Assert.Equal(reconciliation.Price, partitionBid.Amount);

            var validBids = await bidRepo.GetBidsMadeDuringPartitionWithinAuctionDeadlineAsync(auction.Id, auctionEndTime);
            Assert.Single(validBids);

            var finalAuction = await auctionService.GetAuctionAsync(auction.Id, ConsistencyLevel.Strong);
            Assert.Equal(15000m, finalAuction.CurrentPrice);


        }
    }
}