namespace CarAuction.Infrastructure.Data
{
    public class BidSequence
    {
        public Guid AuctionId { get; set; }
        public long CurrentSequence { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
    