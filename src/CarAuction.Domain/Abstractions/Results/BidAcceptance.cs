namespace CarAuction.Domain.Abstractions.Results
{
    public class BidAcceptance
    {
        public bool IsValid { get; set; }
        public string Reason { get; set; } = string.Empty;
        public long AssignedSequence { get; set; }
    }
}
