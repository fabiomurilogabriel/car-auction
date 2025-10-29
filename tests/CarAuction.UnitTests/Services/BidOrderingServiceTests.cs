using CarAuction.Application.Services;
using CarAuction.Domain.Abstractions.Repositories;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Auctions;
using CarAuction.Domain.Models.Bids;
using Moq;

namespace CarAuction.UnitTests.Services
{
    public class BidOrderingServiceTests
    {
        private readonly Mock<IBidRepository> _mockBidRepository;
        private readonly Mock<IAuctionRepository> _mockAuctionRepository;
        private readonly BidOrderingService _bidOrderingService;

        public BidOrderingServiceTests()
        {
            _mockBidRepository = new Mock<IBidRepository>();
            _mockAuctionRepository = new Mock<IAuctionRepository>();
            _bidOrderingService = new BidOrderingService(_mockBidRepository.Object, _mockAuctionRepository.Object);
        }

        [Fact]
        public async Task GetNextBidSequenceAsync_ShouldReturnIncrementedSequence()
        {
            // Arrange
            var auctionId = Guid.NewGuid();
            _mockBidRepository.Setup(x => x.GetNextSequenceAsync(auctionId))
                .ReturnsAsync(5);

            // Act
            var sequence = await _bidOrderingService.GetNextBidSequenceAsync(auctionId);

            // Assert
            Assert.Equal(5, sequence);
            _mockBidRepository.Verify(x => x.GetNextSequenceAsync(auctionId), Times.Once);
        }

        [Fact]
        public async Task ValidateBidOrderAsync_WithValidBid_ShouldReturnValid()
        {
            // Arrange
            var auctionId = Guid.NewGuid();
            var auction = CreateTestAuction(auctionId, 10000m); // Current price 10000
            var bid = new Bid(auctionId, Guid.NewGuid(), 12000m, Region.USEast, 1); // Higher bid

            _mockAuctionRepository.Setup(x => x.GetByIdAsync(auctionId))
                .ReturnsAsync(auction);

            // Act
            var result = await _bidOrderingService.ValidateBidOrderAsync(auctionId, bid);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Reason);
        }

        [Fact]
        public async Task ValidateBidOrderAsync_WithLowerAmount_ShouldReturnInvalid()
        {
            // Arrange
            var auctionId = Guid.NewGuid();
            var auction = CreateTestAuction(auctionId, 10000m); // Current price 10000
            var bid = new Bid(auctionId, Guid.NewGuid(), 9000m, Region.USEast, 1); // Lower bid

            _mockAuctionRepository.Setup(x => x.GetByIdAsync(auctionId))
                .ReturnsAsync(auction);

            // Act
            var result = await _bidOrderingService.ValidateBidOrderAsync(auctionId, bid);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("must be higher than current price", result.Reason);
        }

        [Fact]
        public async Task ValidateBidOrderAsync_WithNonExistentAuction_ShouldReturnInvalid()
        {
            // Arrange
            var auctionId = Guid.NewGuid();
            var bid = new Bid(auctionId, Guid.NewGuid(), 12000m, Region.USEast, 1);

            _mockAuctionRepository.Setup(x => x.GetByIdAsync(auctionId))
                .ReturnsAsync((Auction)null);

            // Act
            var result = await _bidOrderingService.ValidateBidOrderAsync(auctionId, bid);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("Auction not found", result.Reason);
        }

        [Theory]
        [InlineData(11000.50)]
        [InlineData(15000.99)]
        [InlineData(100000.00)]
        public async Task ValidateBidOrderAsync_WithValidAmounts_ShouldReturnValid(decimal amount)
        {
            // Arrange
            var auctionId = Guid.NewGuid();
            var auction = CreateTestAuction(auctionId, 10000m); // Current price 10000
            var bid = new Bid(auctionId, Guid.NewGuid(), amount, Region.USEast, 1);

            _mockAuctionRepository.Setup(x => x.GetByIdAsync(auctionId))
                .ReturnsAsync(auction);

            // Act
            var result = await _bidOrderingService.ValidateBidOrderAsync(auctionId, bid);

            // Assert
            Assert.True(result.IsValid);
        }

        private static Auction CreateTestAuction(Guid auctionId, decimal currentPrice)
        {
            var auction = new Auction(
                Guid.NewGuid(),
                Region.USEast,
                currentPrice,
                15000m,
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(1)
            );
            
            // Use reflection to set Id and CurrentPrice for testing
            var idField = typeof(Auction).GetField("<Id>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            idField?.SetValue(auction, auctionId);
            
            return auction;
        }
    }
}