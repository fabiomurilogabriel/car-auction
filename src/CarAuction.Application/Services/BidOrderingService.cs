using CarAuction.Domain.Abstractions;
using CarAuction.Domain.Abstractions.Repositories;
using CarAuction.Domain.Abstractions.Results;
using CarAuction.Domain.Models.Bids;

namespace CarAuction.Application.Services
{
    public class BidOrderingService(
        IBidRepository bidRepository,
        IAuctionRepository auctionRepository) : IBidOrderingService
    {
        private readonly IBidRepository _bidRepository = bidRepository;
        private readonly IAuctionRepository _auctionRepository = auctionRepository;

        public async Task<long> GetNextBidSequenceAsync(Guid auctionId)
        {
            try
            {
                return await _bidRepository.GetNextSequenceAsync(auctionId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in GetNextBidSequenceAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<BidAcceptance> ValidateBidOrderAsync(Guid auctionId, Bid bid)
        {
            try
            {
                var auction = await _auctionRepository.GetByIdAsync(auctionId);

                if (auction is null)
                {
                    return new BidAcceptance
                    {
                        IsValid = false,
                        Reason = "Auction not found"
                    };
                }

                // lance deve ser maior que valor atual do lance do leilao
                if (bid.Amount <= auction.CurrentPrice)
                {
                    return new BidAcceptance
                    {
                        IsValid = false,
                        Reason = $"Bid amount must be higher than current price of {auction.CurrentPrice}"
                    };
                }

                return new BidAcceptance
                {
                    IsValid = true,
                    AssignedSequence = bid.Sequence
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error validating bid order: {ex.Message}");
                throw;
            }
        }

        public async Task<IEnumerable<Bid>> GetOrderedBidsAsync(Guid auctionId, DateTime? since = null)
        {
            try
            {
                var bids = await _bidRepository.GetByAuctionIdAsync(auctionId);

                if (since.HasValue)
                {
                    bids = bids.Where(b => b.CreatedAt >= since.Value);
                }

                return bids.OrderBy(b => b.Sequence).ThenBy(b => b.CreatedAt);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error retrieving ordered bids: {ex.Message}");
                throw;
            }
        }
    }
}
