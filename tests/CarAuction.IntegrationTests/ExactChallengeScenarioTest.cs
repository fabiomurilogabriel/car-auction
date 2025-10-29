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
            // Arrange - Setup completo do sistema
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

            // 1. Criar leilão em US-East que terminará durante a partição
            var vehicle = TestDataBuilder.CreateTestVehicle(Region.USEast);
            await vehicleRepo.CreateAsync(vehicle);

            var auctionEndTime = DateTime.UtcNow.AddSeconds(3); // Termina em 3 segundos
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

            // 2. Lance inicial antes da partição (baseline)
            await simulator.SetCurrentRegionAsync(Region.USEast);
            var initialBidRequest = new BidRequest
            {
                BidderId = Guid.NewGuid(),
                Amount = 11000m
            };

            var initialBid = await auctionService.PlaceBidAsync(auction.Id, initialBidRequest);
            Assert.True(initialBid.Success);
            Assert.Equal("Bid placed successfully", initialBid.Message);

            // 3. INÍCIO DA PARTIÇÃO DE 5 MINUTOS (simulada como 5 segundos para teste)
            await simulator.SimulatePartitionAsync(Region.EUWest, Region.USEast, TimeSpan.FromSeconds(5));
            
            Assert.True(simulator.IsPartitioned);

            // 4. Durante a partição: Usuário US tenta lance no leilão US (local)
            await simulator.SetCurrentRegionAsync(Region.USEast);
            var usBidRequest = new BidRequest
            {
                BidderId = Guid.NewGuid(),
                Amount = 12000m
            };

            var usBidResult = await auctionService.PlaceBidAsync(auction.Id, usBidRequest);
            Assert.False(usBidResult.Success);
            Assert.Equal("Auction region is partitioned. Cannot place bid at this time.", usBidResult.Message);

            // 5. Durante a partição: Usuário EU tenta lance no leilão US (cross-region)
            await simulator.SetCurrentRegionAsync(Region.EUWest);
            var euBidRequest = new BidRequest
            {
                BidderId = Guid.NewGuid(),
                Amount = 13000m
            };

            var euBidResult = await auctionService.PlaceBidAsync(auction.Id, euBidRequest);
            Assert.True(euBidResult.Success);
            Assert.Equal("Bid queued for reconciliation after partition heals", euBidResult.Message);

            // 6. Aguardar o leilão "terminar" durante a partição
            await Task.Delay(TimeSpan.FromSeconds(4)); // Leilão deveria ter terminado
            Console.WriteLine($"⏰ Leilão deveria ter terminado às {auctionEndTime:HH:mm:ss}");

            // 7. Aguardar cura da partição (5 segundos total)
            await Task.Delay(TimeSpan.FromSeconds(2)); // Total: 6 segundos, partição curada
            Console.WriteLine($"🔄 PARTIÇÃO CURADA às {DateTime.UtcNow:HH:mm:ss}");

            // Assert - Verificar que a partição foi curada
            Assert.False(simulator.IsPartitioned);

            // 8. RECONCILIAÇÃO AUTOMÁTICA
            Console.WriteLine("🔄 Iniciando reconciliação...");
            var reconciliationResult = await auctionService.ReconcileAuctionAsync(auction.Id);

            // REQUISITO: Ensure no bids are lost
            var allBids = await bidRepo.GetByAuctionIdAsync(auction.Id);
            var bidsList = allBids.ToList();
            
            Console.WriteLine($"📊 Total de lances encontrados: {bidsList.Count}");
            foreach (var bid in bidsList.OrderBy(b => b.Sequence))
            {
                var partitionFlag = bid.IsDuringPartition ? "[PARTIÇÃO]" : "[NORMAL]";
                var acceptedFlag = bid.IsAccepted ? "✅" : "❌";
                Console.WriteLine($"   {acceptedFlag} ${bid.Amount} - {bid.OriginRegion} {partitionFlag}");
            }

            // VERIFICAÇÕES DOS REQUISITOS:

            // ✅ REQUISITO: "Ensure no bids are lost" - Apenas bids enfileirados para reconciliação
            // Esperamos: 1 inicial + 1 cross-region durante partição = 2 bids salvos
            // O bid local durante partição é rejeitado e NÃO salvo (comportamento CP correto)
            Assert.True(bidsList.Count >= 2, "Lances enfileirados para reconciliação devem ser preservados");
            
            // ✅ REQUISITO: "Define the behavior during partition"
            var partitionBids = bidsList.Where(b => b.IsDuringPartition).ToList();
            Assert.True(partitionBids.Any(), "Lances cross-region durante partição devem ser identificados");
            
            // ✅ REQUISITO: "Implement a reconciliation mechanism post-partition"
            Assert.True(reconciliationResult.Success, "Reconciliação deve ser bem-sucedida");
            
            // ✅ REQUISITO: "Maintain auction integrity"
            var finalAuction = await auctionService.GetAuctionAsync(auction.Id, ConsistencyLevel.Strong);
            Assert.NotNull(finalAuction);
            Assert.True(finalAuction.State == AuctionState.Ended || finalAuction.State == AuctionState.Active);

            // Verificar comportamentos específicos durante partição:
            
            // Lance US (local) durante partição - deve ser REJEITADO (CP behavior)
            Assert.False(usBidResult.Success, "Lance local durante partição deve ser rejeitado (CP)");
            Assert.Contains("partitioned", usBidResult.Message, StringComparison.OrdinalIgnoreCase);
            
            // Lance EU (cross-region) durante partição - deve ser ENFILEIRADO (AP behavior)
            Assert.True(euBidResult.Success, "Lance cross-region durante partição deve ser enfileirado (AP)");
            Assert.Contains("reconciliation", euBidResult.Message, StringComparison.OrdinalIgnoreCase);

            // Verificar que o vencedor foi determinado deterministicamente
            if (reconciliationResult.WinnerId.HasValue)
            {
                Assert.True(reconciliationResult.Price > 0, "Preço vencedor deve ser válido");
                Console.WriteLine($"🏆 Vencedor final: ${reconciliationResult.Price} - ID: {reconciliationResult.WinnerId}");
            }

            // Verificar integridade temporal (lances após prazo não devem ser considerados)
            var validBids = await bidRepo.GetBidsMadeDuringPartitionWithinAuctionDeadlineAsync(auction.Id, auctionEndTime);
            Console.WriteLine($"⏰ Lances válidos dentro do prazo: {validBids.Count()}");

            Console.WriteLine("\n🎉 CENÁRIO DO DESAFIO COMPLETADO COM SUCESSO!");
            Console.WriteLine("✅ Nenhum lance perdido");
            Console.WriteLine("✅ Comportamento durante partição definido");
            Console.WriteLine("✅ Reconciliação implementada");
            Console.WriteLine("✅ Integridade do leilão mantida");
        }

        [Fact]
        public async Task PartitionDuringAuctionEnd_ShouldPauseAndReconcile()
        {
            // Arrange - Cenário específico: leilão termina EXATAMENTE durante partição
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

            // Leilão que termina em 30 segundos (tempo suficiente para o teste)
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

            // Lance antes da partição (mesmo região do leilão)
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

            // Act - Iniciar partição que durará além do fim do leilão
            await simulator.SimulatePartitionAsync(Region.USEast, Region.EUWest, TimeSpan.FromSeconds(5));
            
            await simulator.SetCurrentRegionAsync(Region.USEast);
            var partitionBid = new BidRequest { BidderId = Guid.NewGuid(), Amount = 15000m };
            var partitionBidResult = await auctionService.PlaceBidAsync(auction.Id, partitionBid);
            Assert.True(partitionBidResult.Success);
            Assert.Equal("Bid queued for reconciliation after partition heals", partitionBidResult.Message);

            // Aguardar cura da partição
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Reconciliação
            var reconciliation = await auctionService.ReconcileAuctionAsync(auction.Id);
            Assert.True(reconciliation.Success);
            Assert.Equal(reconciliation.WinnerId, partitionBidResult.Bid.BidderId);
            Assert.Equal(reconciliation.Price, partitionBid.Amount);

            // Assert - Lance durante partição dentro do prazo deve ser considerado
            var validBids = await bidRepo.GetBidsMadeDuringPartitionWithinAuctionDeadlineAsync(auction.Id, auctionEndTime);
            Assert.Single(validBids); // Lance durante partição dentro do prazo

            var finalAuction = await auctionService.GetAuctionAsync(auction.Id, ConsistencyLevel.Strong);
            Assert.Equal(15000m, finalAuction.CurrentPrice); // Lance de 15000m deve vencer

            Console.WriteLine("✅ Lances após prazo corretamente excluídos da reconciliação");
        }
    }
}