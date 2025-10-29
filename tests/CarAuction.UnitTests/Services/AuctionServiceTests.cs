using CarAuction.Application.Services;
using CarAuction.Domain.Abstractions;
using CarAuction.Domain.Abstractions.Repositories;
using CarAuction.Domain.Abstractions.Requests;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Auctions;
using Moq;

namespace CarAuction.UnitTests.Services
{
    public class AuctionServiceTests
    {
        private readonly Mock<IAuctionRepository> _mockAuctionRepository;
        private readonly Mock<IBidRepository> _mockBidRepository;
        private readonly Mock<IBidOrderingService> _mockBidOrderingService;
        private readonly Mock<IRegionCoordinator> _mockRegionCoordinator;
        private readonly Mock<IConflictResolver> _mockConflictResolver;
        private readonly Mock<IPartitionSimulator> _mockPartitionSimulator;
        private readonly AuctionService _auctionService;

        public AuctionServiceTests()
        {
            _mockAuctionRepository = new Mock<IAuctionRepository>();
            _mockBidRepository = new Mock<IBidRepository>();
            _mockBidOrderingService = new Mock<IBidOrderingService>();
            _mockRegionCoordinator = new Mock<IRegionCoordinator>();
            _mockConflictResolver = new Mock<IConflictResolver>();
            _mockPartitionSimulator = new Mock<IPartitionSimulator>();

            _auctionService = new AuctionService(
                _mockAuctionRepository.Object,
                _mockBidRepository.Object,
                _mockBidOrderingService.Object,
                _mockRegionCoordinator.Object,
                _mockConflictResolver.Object,
                _mockPartitionSimulator.Object
            );
        }

        [Fact]
        public async Task CreateAuctionAsync_ShouldCreateAndStartAuction()
        {
            // Arrange
            var request = new CreateAuctionRequest
            {
                VehicleId = Guid.NewGuid(),
                Region = Region.USEast,
                StartingPrice = 10000m,
                ReservePrice = 15000m,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddHours(1)
            };

            _mockAuctionRepository.Setup(x => x.CreateAsync(It.IsAny<Auction>()))
                .Returns(Task.FromResult(Guid.NewGuid()));

            // Act
            var result = await _auctionService.CreateAuctionAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(request.VehicleId, result.VehicleId);
            Assert.Equal(request.Region, result.Region);
            Assert.Equal(request.StartingPrice, result.StartingPrice);
            Assert.Equal(AuctionState.Active, result.State);
            _mockAuctionRepository.Verify(x => x.CreateAsync(It.IsAny<Auction>()), Times.Once);
        }

        [Fact]
        public async Task PlaceBidAsync_WithNonExistentAuction_ShouldReturnFailure()
        {
            // Arrange
            var auctionId = Guid.NewGuid();
            var bidRequest = new BidRequest
            {
                BidderId = Guid.NewGuid(),
                Amount = 12000m
            };

            _mockAuctionRepository.Setup(x => x.GetWithBidsAsync(auctionId))
                .ReturnsAsync((Auction)null);

            // Act
            var result = await _auctionService.PlaceBidAsync(auctionId, bidRequest);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Auction not found", result.Message);
        }

        [Fact]
        public async Task PlaceBidAsync_WithExpiredAuction_ShouldEndAuction()
        {
            // Arrange
            var auction = CreateExpiredAuction();
            
            var bidRequest = new BidRequest
            {
                BidderId = Guid.NewGuid(),
                Amount = 12000m
            };

            _mockAuctionRepository.Setup(x => x.GetWithBidsAsync(auction.Id))
                .ReturnsAsync(auction);
            _mockAuctionRepository.Setup(x => x.UpdateAsync(It.IsAny<Auction>()))
                .ReturnsAsync(true);

            // Act
            var result = await _auctionService.PlaceBidAsync(auction.Id, bidRequest);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Auction ended successfully", result.Message);
            _mockAuctionRepository.Verify(x => x.UpdateAsync(It.IsAny<Auction>()), Times.Once);
        }

        [Fact]
        public async Task GetAuctionAsync_WithStrongConsistency_ShouldCallGetWithBids()
        {
            // Arrange
            var auctionId = Guid.NewGuid();
            var auction = CreateTestAuction();

            _mockAuctionRepository.Setup(x => x.GetWithBidsAsync(auctionId))
                .ReturnsAsync(auction);

            // Act
            var result = await _auctionService.GetAuctionAsync(auctionId, ConsistencyLevel.Strong);

            // Assert
            Assert.NotNull(result);
            _mockAuctionRepository.Verify(x => x.GetWithBidsAsync(auctionId), Times.Once);
            _mockAuctionRepository.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task GetAuctionAsync_WithEventualConsistency_ShouldCallGetById()
        {
            // Arrange
            var auctionId = Guid.NewGuid();
            var auction = CreateTestAuction();

            _mockAuctionRepository.Setup(x => x.GetByIdAsync(auctionId))
                .ReturnsAsync(auction);

            // Act
            var result = await _auctionService.GetAuctionAsync(auctionId, ConsistencyLevel.Eventual);

            // Assert
            Assert.NotNull(result);
            _mockAuctionRepository.Verify(x => x.GetByIdAsync(auctionId), Times.Once);
            _mockAuctionRepository.Verify(x => x.GetWithBidsAsync(It.IsAny<Guid>()), Times.Never);
        }

        private static Auction CreateTestAuction()
        {
            var auction = new Auction(
                Guid.NewGuid(),
                Region.USEast,
                10000m,
                15000m,
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(1)
            );
            auction.Start();
            return auction;
        }

        private static Auction CreateExpiredAuction()
        {
            var auction = new Auction(
                Guid.NewGuid(),
                Region.USEast,
                10000m,
                15000m,
                DateTime.UtcNow.AddHours(-2),
                DateTime.UtcNow.AddHours(-1) // Expired 1 hour ago
            );
            auction.Start();
            return auction;
        }
    }
}