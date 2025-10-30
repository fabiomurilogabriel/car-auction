using CarAuction.Application.Services;
using CarAuction.Domain.Abstractions.Requests;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Auctions;
using CarAuction.Infrastructure.Data.Repositories;
using CarAuction.Infrastructure.Services;
using CarAuction.IntegrationTests.Helpers;

namespace CarAuction.IntegrationTests
{
    public class CAPConsistencyTests
    {
        [Fact]
        public async Task LocalBid_ShouldUseStrongConsistency_CP()
        {
            // Arrange - Setup completo dos serviços
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

            // Criar veículo e leilão
            var vehicle = TestDataBuilder.CreateTestVehicle(Region.USEast);
            await vehicleRepo.CreateAsync(vehicle);

            var createRequest = new CreateAuctionRequest
            {
                VehicleId = vehicle.Id,
                Region = Region.USEast,
                StartingPrice = 10000m,
                ReservePrice = 15000m,
                StartTime = DateTime.UtcNow.AddMinutes(1),
                EndTime = DateTime.UtcNow.AddHours(1)
            };

            var auction = await auctionService.CreateAuctionAsync(createRequest);

            // Simular região atual como US-East
            await simulator.SetCurrentRegionAsync(Region.USEast);

            // Act - Lance local (mesma região do leilão)
            var bidRequest = new BidRequest
            {
                BidderId = Guid.NewGuid(),
                Amount = 11000m
            };

            var result = await auctionService.PlaceBidAsync(auction.Id, bidRequest);

            // Assert - CP: Consistência forte garantida
            Assert.True(result.Success);
            Assert.NotNull(result.Bid);
            Assert.True(result.Bid.IsAccepted);
            Assert.False(result.Bid.IsDuringPartition);

            // Verificar que o lance foi processado imediatamente com consistência forte
            var updatedAuction = await auctionService.GetAuctionAsync(auction.Id, ConsistencyLevel.Strong);
            Assert.Equal(11000m, updatedAuction.CurrentPrice);
            Assert.Equal(bidRequest.BidderId, updatedAuction.WinningBidderId);
        }

        [Fact]
        public async Task CrossRegionBid_DuringPartition_ShouldUseEventualConsistency_AP()
        {
            // Arrange
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

            // Criar leilão em US-East
            var vehicle = TestDataBuilder.CreateTestVehicle(Region.USEast);
            await vehicleRepo.CreateAsync(vehicle);

            var createRequest = new CreateAuctionRequest
            {
                VehicleId = vehicle.Id,
                Region = Region.USEast,
                StartingPrice = 10000m,
                ReservePrice = 15000m,
                StartTime = DateTime.UtcNow.AddMinutes(1),
                EndTime = DateTime.UtcNow.AddHours(1)
            };

            var auction = await auctionService.CreateAuctionAsync(createRequest);

            // Simular partição entre regiões
            await simulator.SimulatePartitionAsync(Region.EUWest, Region.USEast, TimeSpan.FromSeconds(5));
            await simulator.SetCurrentRegionAsync(Region.EUWest);

            // Act - Lance cross-region durante partição
            var bidRequest = new BidRequest
            {
                BidderId = Guid.NewGuid(),
                Amount = 12000m
            };

            var result = await auctionService.PlaceBidAsync(auction.Id, bidRequest);

            // Assert - AP: Disponibilidade mantida, consistência eventual
            Assert.True(result.Success);
            Assert.NotNull(result.Bid);
            Assert.True(result.Bid.IsDuringPartition); // Marcado para reconciliação
            Assert.Contains("queued for reconciliation", result.Message);

            // Verificar que o leilão foi pausado (comportamento durante partição)
            var pausedAuction = await auctionService.GetAuctionAsync(auction.Id, ConsistencyLevel.Eventual);
            Assert.Equal(AuctionState.Paused, pausedAuction.State);

            // Verificar que o lance foi enfileirado, não processado imediatamente
            var partitionBids = await bidRepo.GetBidsMadeDuringPartitionWithinAuctionDeadlineAsync(auction.Id, auction.EndTime);
            Assert.Single(partitionBids);
            Assert.Equal(12000m, partitionBids.First().Amount);
        }

        [Fact]
        public async Task AuctionCreation_ShouldAlwaysUseStrongConsistency_CP()
        {
            // Arrange
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

            // Simular partição ativa
            await simulator.SimulatePartitionAsync(Region.USEast, Region.EUWest, TimeSpan.FromSeconds(5));

            // Act - Criar leilão mesmo durante partição
            var createRequest = new CreateAuctionRequest
            {
                VehicleId = vehicle.Id,
                Region = Region.USEast,
                StartingPrice = 10000m,
                ReservePrice = 15000m,
                StartTime = DateTime.UtcNow.AddMinutes(1),
                EndTime = DateTime.UtcNow.AddHours(1)
            };

            var auction = await auctionService.CreateAuctionAsync(createRequest);

            // Assert - CP: Criação sempre requer consistência forte
            Assert.NotNull(auction);
            Assert.Equal(AuctionState.Active, auction.State);
            Assert.Equal(10000m, auction.CurrentPrice);

            // Verificar que BidSequence foi criada atomicamente
            var sequence = await bidRepo.GetNextSequenceAsync(auction.Id);
            Assert.Equal(1, sequence);
        }

        [Fact]
        public async Task ConsistencyLevel_ShouldAffectReadBehavior()
        {
            // Arrange
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

            var createRequest = new CreateAuctionRequest
            {
                VehicleId = vehicle.Id,
                Region = Region.USEast,
                StartingPrice = 10000m,
                ReservePrice = 15000m,
                StartTime = DateTime.UtcNow.AddMinutes(1),
                EndTime = DateTime.UtcNow.AddHours(1)
            };

            var auction = await auctionService.CreateAuctionAsync(createRequest);

            // Act & Assert - Diferentes níveis de consistência
            
            // Strong Consistency: Dados mais atualizados, com lances
            var strongRead = await auctionService.GetAuctionAsync(auction.Id, ConsistencyLevel.Strong);
            Assert.NotNull(strongRead);
            Assert.NotNull(strongRead.Bids); // Inclui lances

            // Eventual Consistency: Dados básicos, sem lances
            var eventualRead = await auctionService.GetAuctionAsync(auction.Id, ConsistencyLevel.Eventual);
            Assert.NotNull(eventualRead);
        }
    }
}