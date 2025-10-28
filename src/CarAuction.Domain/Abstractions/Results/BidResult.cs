using CarAuction.Domain.Models.Bids;

namespace CarAuction.Domain.Abstractions.Results
{
    public class BidResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Bid Bid { get; set; } 
    }
}
