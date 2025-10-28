using CarAuction.Domain.Models.Auctions;
using CarAuction.Domain.Models.Bids;
using CarAuction.Domain.Models.Vehicles;

namespace CarAuction.Domain.Models.Auctions
{
    public class Auction
    {
        public Guid Id { get; private set; }
        public Guid VehicleId { get; private set; }
        public Vehicle? Vehicle { get; private set; }
        public Region Region { get; private set; }
        public AuctionState State { get; private set; }
        public decimal StartingPrice { get; private set; }
        public decimal? ReservePrice { get; private set; }
        public decimal CurrentPrice { get; private set; }
        public Guid? WinningBidderId { get; private set; }
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public long Version { get; private set; }
        private readonly List<Bid> _bids = [];
        public IReadOnlyCollection<Bid> Bids => _bids.AsReadOnly();

        public Auction(
            Guid vehicleId,
            Region region,
            decimal startingPrice,
            decimal? reservePrice,
            DateTime startTime,
            DateTime endTime)
        {
            Id = Guid.NewGuid();
            VehicleId = vehicleId;
            Region = region;
            StartingPrice = startingPrice;
            ReservePrice = reservePrice;
            CurrentPrice = startingPrice;
            StartTime = startTime;
            EndTime = endTime;
            State = AuctionState.Draft;
            CreatedAt = DateTime.UtcNow;
            Version = 0;
        }

        private Auction() { }

        public void UpdateTimes(DateTime startTime, DateTime endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdatePricing(decimal startingPrice, decimal? reservePrice)
        {
            StartingPrice = startingPrice;
            ReservePrice = reservePrice;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Start()
        {
            if (State != AuctionState.Draft)
                throw new InvalidOperationException("Can only start auctions in draft state");

            State = AuctionState.Active;
            CurrentPrice = StartingPrice;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Pause()
        {
            if (State != AuctionState.Active)
                throw new InvalidOperationException("Can only pause active auctions");

            State = AuctionState.Paused;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Resume()
        {
            if (State != AuctionState.Paused)
                throw new InvalidOperationException("Can only resume paused auctions");

            State = AuctionState.Active;
            UpdatedAt = DateTime.UtcNow;
        }

        public void End()
        {
            if (State != AuctionState.Active && State != AuctionState.Paused)
                throw new InvalidOperationException("Can only end active or paused auctions");

            State = AuctionState.Ended;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Cancel()
        {
            if (State == AuctionState.Ended)
                throw new InvalidOperationException("Cannot cancel ended auction");

            State = AuctionState.Cancelled;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool TryPlaceBid(Bid bid)
        {
            if (State is not AuctionState.Active)
                return false;

            if (bid.Amount <= CurrentPrice)
                return false;

            CurrentPrice = bid.Amount;
            WinningBidderId = bid.BidderId;

            _bids.Add(bid);
            
            UpdatedAt = DateTime.UtcNow;
            Version++;

            return true;
        }

        public void UpdateWinningBid(decimal amount, Guid bidderId)
        {
            CurrentPrice = amount;
            WinningBidderId = bidderId;
            UpdatedAt = DateTime.UtcNow;
            Version++;
        }

        public void AddBid(Bid bid)
        {
            _bids.Add(bid);
        }

        public void SetVehicle(Vehicle vehicle)
        {
            Vehicle = vehicle;
        }
    }
}
