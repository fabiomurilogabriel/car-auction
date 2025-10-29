using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Auctions;
using CarAuction.Infrastructure.Data.Repositories;
using CarAuction.Infrastructure.Services;
using CarAuction.IntegrationTests.Helpers;

namespace CarAuction.IntegrationTests
{
    public class PartitionSimulationTests
    {
        [Fact]
        public async Task CompletePartitionSimulation_ShouldNotLoseBids_And_ReconcileCorrectWinner()
        {
            // Arrange - same in-memory context for all components
            var context = TestDbContextFactory.CreateInMemoryContext();
            var auctionRepo = new AuctionRepository(context);
            var bidRepo = new BidRepository(context);
            var partitionRepo = new PartitionEventRepository(context);
            var simulator = new PartitionSimulator(partitionRepo);

            // Create vehicle and auction in US-East
            var vehicle = TestDataBuilder.CreateTestVehicle(Region.USEast);
            context.Vehicles.Add(vehicle);
            await context.SaveChangesAsync();

            var auction = TestDataBuilder.CreateTestAuction(vehicle.Id, Region.USEast);
            var auctionId = await auctionRepo.CreateAsync(auction);

            // Start auction and set end time so it will end during partition
            auction = await auctionRepo.GetByIdAsync(auctionId);
            auction.Start();
            // Set auction end to soon so it "ends" while partition is active
            auction.UpdateTimes(auction.StartTime, DateTime.UtcNow.AddSeconds(1));
            await auctionRepo.UpdateAsync(auction);

            // Pre-partition bid (accepted before partition)
            var prePartitionBid = TestDataBuilder.CreateTestBid(auctionId, 10000m, Region.USEast, 1);
            prePartitionBid.Accept();
            await bidRepo.CreateAsync(prePartitionBid);

            // Act - start partition that will last longer than auction end (short duration for test)
            var partitionDuration = TimeSpan.FromSeconds(2);
            await simulator.SimulatePartitionAsync(originRegion: Region.EUWest, auctionRegion: Region.USEast, duration: partitionDuration);

            Assert.True(simulator.IsPartitioned);

            // During partition: local (US-East) bid -> accepted locally
            var localDuringPartition = TestDataBuilder.CreateTestBid(auctionId, 11000m, Region.USEast, 2);
            localDuringPartition.Accept();
            await bidRepo.CreateAsync(localDuringPartition);

            // During partition: remote (EU-West) cross-region bid -> marked as during partition (will be reconciled later)
            var remoteDuringPartition = TestDataBuilder.CreateTestBid(auctionId, 12000m, Region.EUWest, 3);
            remoteDuringPartition.MarkAsDuringPartition();
            await bidRepo.CreateAsync(remoteDuringPartition);

            // Wait for partition auto-heal to occur
            await Task.Delay(partitionDuration + TimeSpan.FromSeconds(1));

            // Assert partition healed
            Assert.False(simulator.IsPartitioned);

            // Verify all bids persisted (no loss)
            var allBids = (await bidRepo.GetByAuctionIdAsync(auctionId)).ToList();
            Assert.Equal(3, allBids.Count);

            // Verify exactly one bid marked as during partition (the cross-region one)
            // 
            var partitionBids = (await bidRepo.GetBidsMadeDuringPartitionWithinAuctionDeadlineAsync(auctionId, auction.EndTime)).ToList();
            Assert.Single(partitionBids);
            Assert.Equal(12000m, partitionBids[0].Amount);
            Assert.True(partitionBids[0].IsDuringPartition);

            // Reconciliation: deterministic selection (amount desc, then created time asc, then sequence asc)
            var bidsForReconciliation = allBids
                .Where(b => b.CreatedAt <= auction.EndTime) // only consider bids before auction end
                .OrderByDescending(b => b.Amount)
                .ThenBy(b => b.CreatedAt)
                .ThenBy(b => b.Sequence)
                .ToList();

            var finalWinner = bidsForReconciliation.FirstOrDefault();
            Assert.NotNull(finalWinner);

            // Apply reconciliation result to auction and persist
            var auctionToUpdate = await auctionRepo.GetByIdAsync(auctionId);
            auctionToUpdate.UpdateWinningBid(finalWinner.Amount, finalWinner.BidderId);
            auctionToUpdate.End();
            await auctionRepo.UpdateAsync(auctionToUpdate);

            // Reload auction and assert final state persisted
            var finalAuction = await auctionRepo.GetByIdAsync(auctionId);
            Assert.NotNull(finalAuction);
            Assert.Equal(finalWinner.BidderId, finalAuction.WinningBidderId);
            Assert.Equal(finalWinner.Amount, finalAuction.CurrentPrice);
            Assert.Equal(AuctionState.Ended, finalAuction.State);

            // Verify no active partition remains for the auction region
            var activePartition = await partitionRepo.GetCurrentPartitionByAuctionRegionAsync(Region.USEast);
            Assert.Null(activePartition);
        }
    }
}