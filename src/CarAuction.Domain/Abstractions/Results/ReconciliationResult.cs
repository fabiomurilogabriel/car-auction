namespace CarAuction.Domain.Abstractions.Results
{
    public class ReconciliationResult
    {
        public bool Success { get; set; }
        public int BidsReconciled { get; set; }
        public Guid? WinnerId { get; set; }
        public decimal? Price { get; set; }
    }
}
