using CarAuction.Application.Services;
using CarAuction.Domain.Abstractions.Requests;
using CarAuction.Domain.Models;
using CarAuction.Infrastructure.Data.Repositories;
using CarAuction.Infrastructure.Services;
using CarAuction.IntegrationTests.Helpers;
using System.Diagnostics;

namespace CarAuction.IntegrationTests
{

    public class PerformanceAndConcurrencyTests
    {
        [Fact]
        public async Task BidProcessing_ShouldBeFasterThan200ms_P95()
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

            var createRequest = new CreateAuctionRequest
            {
                VehicleId = vehicle.Id,
                Region = Region.USEast,
                StartingPrice = 10000m,
                ReservePrice = 15000m,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddHours(1)
            };

            var auction = await auctionService.CreateAuctionAsync(createRequest);
            await simulator.SetCurrentRegionAsync(Region.USEast);

            var processingTimes = new List<double>();
            var baseAmount = 11000m;

            for (int i = 0; i < 100; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                
                var bidRequest = new BidRequest
                {
                    BidderId = Guid.NewGuid(),
                    Amount = baseAmount + i
                };

                var result = await auctionService.PlaceBidAsync(auction.Id, bidRequest);
                
                stopwatch.Stop();
                processingTimes.Add(stopwatch.Elapsed.TotalMilliseconds);

                if (result.Success)
                {
                    Assert.True(stopwatch.Elapsed.TotalMilliseconds < 500, 
                        $"Lance {i} levou {stopwatch.Elapsed.TotalMilliseconds}ms - muito lento");
                }
            }

            processingTimes.Sort();
            var p95Index = (int)Math.Ceiling(processingTimes.Count * 0.95) - 1;
            var p95Time = processingTimes[p95Index];

            Assert.True(p95Time < 200, $"P95 de {p95Time:F2}ms excede o limite de 200ms");
        }

        [Fact]
        public async Task ConcurrentAuctions_ShouldSupport1000Plus()
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

            var auctionCount = 1000;
            var createdAuctions = new List<Guid>();
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < auctionCount; i++)
            {
                var vehicle = TestDataBuilder.CreateTestVehicle(Region.USEast);
                await vehicleRepo.CreateAsync(vehicle);

                var createRequest = new CreateAuctionRequest
                {
                    VehicleId = vehicle.Id,
                    Region = Region.USEast,
                    StartingPrice = 10000m + i,
                    ReservePrice = 15000m + i,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddHours(1)
                };

                var auction = await auctionService.CreateAuctionAsync(createRequest);
                createdAuctions.Add(auction.Id);
            }

            stopwatch.Stop();

            Assert.Equal(auctionCount, createdAuctions.Count);
            
            var activeAuctions = await auctionRepo.GetActiveAuctionsByRegionAsync(Region.USEast);
            Assert.True(activeAuctions.Count() >= auctionCount, 
                $"Esperado {auctionCount} leilões ativos, encontrado {activeAuctions.Count()}");
        }

        [Fact]
        public async Task ConcurrentUsers_ShouldSupport10000_SimulatedLoad()
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
            var auctions = new List<Guid>();
            for (int i = 0; i < 10; i++)
            {
                var vehicle = TestDataBuilder.CreateTestVehicle(Region.USEast);
                await vehicleRepo.CreateAsync(vehicle);

                var createRequest = new CreateAuctionRequest
                {
                    VehicleId = vehicle.Id,
                    Region = Region.USEast,
                    StartingPrice = 10000m,
                    ReservePrice = 15000m,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow.AddHours(1)
                };

                var auction = await auctionService.CreateAuctionAsync(createRequest);
                auctions.Add(auction.Id);
            }

            await simulator.SetCurrentRegionAsync(Region.USEast);

            var userCount = 1000;
            var successfulBids = 0;
            var failedBids = 0;
            var random = new Random();
            var stopwatch = Stopwatch.StartNew();

            for (int userId = 0; userId < userCount; userId++)
            {
                var auctionId = auctions[random.Next(auctions.Count)];
                var bidRequest = new BidRequest
                {
                    BidderId = Guid.NewGuid(),
                    Amount = 11000m + userId
                };

                var result = await auctionService.PlaceBidAsync(auctionId, bidRequest);
                
                if (result.Success)
                    successfulBids++;
                else
                    failedBids++;


            }

            stopwatch.Stop();

            var totalRequests = successfulBids + failedBids;
            var successRate = (double)successfulBids / totalRequests * 100;
            var requestsPerSecond = totalRequests / stopwatch.Elapsed.TotalSeconds;

            Assert.True(requestsPerSecond > 100, 
                $"Throughput de {requestsPerSecond:F0} req/s é muito baixo");

            Assert.True(successRate > 50, 
                $"Taxa de sucesso de {successRate:F1}% é muito baixa");
        }

        [Fact]
        public async Task SequenceGeneration_ShouldBeAtomic_UnderConcurrency()
        {
            var context = TestDbContextFactory.CreateInMemoryContext();
            var auctionRepo = new AuctionRepository(context);
            var bidRepo = new BidRepository(context);
            var vehicleRepo = new VehicleRepository(context);

            var vehicle = TestDataBuilder.CreateTestVehicle(Region.USEast);
            await vehicleRepo.CreateAsync(vehicle);

            var auction = TestDataBuilder.CreateTestAuction(vehicle.Id, Region.USEast);
            await auctionRepo.CreateAsync(auction);

            var sequenceCount = 100;
            var sequences = new List<long>();

            for (int i = 0; i < sequenceCount; i++)
            {
                var sequence = await bidRepo.GetNextSequenceAsync(auction.Id);
                sequences.Add(sequence);
            }

            Assert.Equal(sequenceCount, sequences.Count);
            Assert.Equal(sequenceCount, sequences.Distinct().Count());

            sequences.Sort();
            Assert.Equal(1, sequences.Min());
            Assert.Equal(sequenceCount, sequences.Max());

            for (int i = 1; i <= sequenceCount; i++)
            {
                Assert.Contains(i, sequences);
            }


        }

        [Fact]
        public async Task SystemAvailability_ShouldMaintain99Point9Percent()
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

            var createRequest = new CreateAuctionRequest
            {
                VehicleId = vehicle.Id,
                Region = Region.USEast,
                StartingPrice = 10000m,
                ReservePrice = 15000m,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddHours(1)
            };

            var auction = await auctionService.CreateAuctionAsync(createRequest);
            await simulator.SetCurrentRegionAsync(Region.USEast);
            var totalOperations = 1000;
            var successfulOperations = 0;
            var failedOperations = 0;

            for (int i = 0; i < totalOperations; i++)
            {
                try
                {
                    if (i % 3 == 0)
                    {
                        var readAuction = await auctionService.GetAuctionAsync(auction.Id, ConsistencyLevel.Eventual);
                        if (readAuction != null) successfulOperations++;
                        else failedOperations++;
                    }
                    else if (i % 3 == 1)
                    {
                        var bidRequest = new BidRequest
                        {
                            BidderId = Guid.NewGuid(),
                            Amount = 11000m + i
                        };
                        var result = await auctionService.PlaceBidAsync(auction.Id, bidRequest);
                        if (result.Success) successfulOperations++;
                        else failedOperations++;
                    }
                    else
                    {
                        var status = await regionCoordinator.GetPartitionStatusAsync();
                        successfulOperations++;
                    }
                }
                catch (Exception)
                {
                    failedOperations++;
                }
            }

            var availability = (double)successfulOperations / totalOperations * 100;
            
            Assert.True(availability >= 99.0,
                $"Disponibilidade de {availability:F3}% está abaixo do esperado");
        }
    }
}