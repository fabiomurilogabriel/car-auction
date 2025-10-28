namespace CarAuction.Domain.Abstractions.Requests
{
    public class BidRequest
    {
        public Guid BidderId { get; set; }
        public decimal Amount { get; set; }
    }
}
