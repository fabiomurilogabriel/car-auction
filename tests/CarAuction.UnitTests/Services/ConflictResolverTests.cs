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
            // Arrange - Simula conflito durante reconciliação pós-partição
            var auctionId = Guid.NewGuid();
            
            // Criar bids com pequeno delay para garantir ordem de tempo
            var bid1 = new Bid(auctionId, Guid.NewGuid(), 10000m, Region.USEast, 1);
            await Task.Delay(1); // Garantir timestamp diferente
            var bid2 = new Bid(auctionId, Guid.NewGuid(), 11000m, Region.USEast, 2);
            await Task.Delay(1);
            var bid3 = new Bid(auctionId, Guid.NewGuid(), 12000m, Region.USEast, 3);
            
            // Marca como bids durante partição
            bid1.MarkAsDuringPartition();
            bid2.MarkAsDuringPartition();
            bid3.MarkAsDuringPartition();
            
            var allBids = new List<Bid> { bid3, bid1, bid2 }; // Ordem aleatória

            // Act - Resolve conflitos por região (cenário crítico do desafio)
            var resolvedBids = await _conflictResolver.ResolveConflictingBidsAsync(allBids, Region.USEast);

            // Assert - Maior valor (bid3) deve vencer por ser ordenado por Amount DESC
            var resolvedList = resolvedBids.ToList();
            Assert.Equal(3, resolvedList.Count);
            
            var acceptedBid = resolvedList.First(b => b.IsAccepted);
            var rejectedBids = resolvedList.Where(b => !b.IsAccepted).ToList();
            
            Assert.Equal(12000m, acceptedBid.Amount); // Maior valor vence
            Assert.Equal(2, rejectedBids.Count);
            Assert.All(rejectedBids, b => Assert.Contains("Lost in conflict resolution", b.RejectionReason));
        }

        [Fact]
        public async Task DetermineFinalWinnerAsync_WithMultipleAcceptedBids_ShouldReturnHighestAmount()
        {
            // Arrange
            var auctionId = Guid.NewGuid();
            
            var bid1 = new Bid(auctionId, Guid.NewGuid(), 10000m, Region.USEast, 1);
            var bid2 = new Bid(auctionId, Guid.NewGuid(), 15000m, Region.EUWest, 2);
            var bid3 = new Bid(auctionId, Guid.NewGuid(), 12000m, Region.USEast, 3);
            
            bid1.Accept();
            bid2.Accept();
            bid3.Accept();
            
            var allBids = new List<Bid> { bid1, bid2, bid3 };

            // Act
            var winner = await _conflictResolver.DetermineFinalWinnerAsync(allBids);

            // Assert
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
            // Arrange
            var auctionId = Guid.NewGuid();
            var bid1 = new Bid(auctionId, Guid.NewGuid(), 10000m, Region.USEast, 1);
            var bid2 = new Bid(auctionId, Guid.NewGuid(), 11000m, Region.EUWest, 2);
            
            // Bids are not accepted
            var allBids = new List<Bid> { bid1, bid2 };

            // Act
            var winner = await _conflictResolver.DetermineFinalWinnerAsync(allBids);

            // Assert
            Assert.Null(winner);
        }

        [Fact]
        public async Task DetermineFinalWinnerAsync_WithHigherRejectedBid_ShouldIgnoreRejectedBids()
        {
            // Arrange - Cenário crítico: lance maior rejeitado não deve vencer
            var auctionId = Guid.NewGuid();
            
            var bid1 = new Bid(auctionId, Guid.NewGuid(), 10000m, Region.USEast, 1);
            var bid2 = new Bid(auctionId, Guid.NewGuid(), 20000m, Region.EUWest, 2); // Maior valor
            var bid3 = new Bid(auctionId, Guid.NewGuid(), 12000m, Region.USEast, 3);
            
            bid1.Accept(); // Aceito
            bid2.Reject("Invalid bid"); // Rejeitado (maior valor mas inválido)
            bid3.Accept(); // Aceito
            
            var allBids = new List<Bid> { bid1, bid2, bid3 };

            // Act
            var winner = await _conflictResolver.DetermineFinalWinnerAsync(allBids);

            // Assert - Bid rejeitado (20000m) não deve vencer, bid3 (12000m) deve vencer
            Assert.NotNull(winner);
            Assert.Equal(12000m, winner.Amount); // Não o maior valor (20000m)
            Assert.Equal(bid3.Id, winner.Id);
            Assert.True(winner.IsAccepted);
            Assert.False(bid1.IsAccepted); // Perdeu para bid3
            Assert.False(bid2.IsAccepted); // Continua rejeitado
        }


        [Fact]
        public async Task DetermineFinalWinnerAsync_OnlyAcceptedBidsCompete_ShouldIgnoreRejected()
        {
            // Arrange
            var auctionId = Guid.NewGuid();
            
            var bid1 = new Bid(auctionId, Guid.NewGuid(), 15000m, Region.USEast, 1);
            var bid2 = new Bid(auctionId, Guid.NewGuid(), 8000m, Region.EUWest, 2);
            
            bid1.Reject("Too high"); // Rejeitado
            bid2.Accept(); // Único aceito
            
            var allBids = new List<Bid> { bid1, bid2 };

            // Act
            var winner = await _conflictResolver.DetermineFinalWinnerAsync(allBids);

            // Assert - Apenas bids aceitos competem
            Assert.NotNull(winner);
            Assert.Equal(8000m, winner.Amount); // Menor valor mas único válido
            Assert.Equal(bid2.Id, winner.Id);
            Assert.True(winner.IsAccepted);
        }
    }
}