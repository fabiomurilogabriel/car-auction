using CarAuction.Domain.Models;

namespace CarAuction.Domain.Abstractions.Requests
{
    public class CreateAuctionRequest
    {
        public Guid VehicleId { get; set; }
        public Region Region { get; set; }
        public decimal StartingPrice { get; set; }
        public decimal? ReservePrice { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}
