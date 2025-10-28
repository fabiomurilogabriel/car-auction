using CarAuction.Domain.Models.Auctions;

namespace CarAuction.Domain.Models.Bids;
public class Bid
{
    public Guid Id { get; private set; }
    public Guid AuctionId { get; private set; }
    public Auction? Auction { get; private set; }
    public Guid BidderId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public long Sequence { get; private set; }
    public Region OriginRegion { get; private set; }
    public bool IsAccepted { get; private set; }
    public string RejectionReason { get; private set; }
    public bool IsDuringPartition { get; private set; }

    public Bid(Guid auctionId, Guid bidderId, decimal amount, Region originRegion, long sequence)
    {
        Id = Guid.NewGuid();
        AuctionId = auctionId;
        BidderId = bidderId;
        Amount = amount;
        OriginRegion = originRegion;
        Sequence = sequence;
        CreatedAt = DateTime.UtcNow;
        IsAccepted = false;
        IsDuringPartition = false;
        RejectionReason = string.Empty;
    }

    private Bid() { }

    public void Accept()
    {
        IsAccepted = true;
        RejectionReason = string.Empty;
    }

    public void Reject(string reason)
    {
        IsAccepted = false;
        RejectionReason = reason;
    }

    public void MarkAsDuringPartition()
    {
        IsDuringPartition = true;
    }

    //public void UpdateSequence(long sequence)
    //{
    //    Sequence = sequence;
    //}

    //public void SetAuction(Auction auction)
    //{
    //    Auction = auction;
    //}
}