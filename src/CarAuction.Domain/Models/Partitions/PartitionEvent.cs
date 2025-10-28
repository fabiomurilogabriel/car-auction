namespace CarAuction.Domain.Models.Partitions
{
    public class PartitionEvent
    {
        public Guid Id { get; private set; }
        public Region OriginBidRegion { get; private set; }
        public Region AuctionRegion { get; private set; }
        public PartitionStatus Status { get; private set; }
        public DateTime? UpdateAt { get; private set; }
        public DateTime? EndTime { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public PartitionEvent(Region originBidRegion, Region auctionRegion)
        {
            Id = Guid.NewGuid();
            OriginBidRegion = originBidRegion;
            AuctionRegion = auctionRegion;
            Status = PartitionStatus.Healthy;
            CreatedAt = DateTime.UtcNow;
        }

        private PartitionEvent() { }

        public void StartPartition()
        {
            Status = PartitionStatus.Partitioned;
            UpdateAt = DateTime.UtcNow;
        }

        public void BeginReconciliation()
        {
            Status = PartitionStatus.Reconciling;
            UpdateAt = DateTime.UtcNow;
        }

        public void Resolve()
        {
            Status = PartitionStatus.Resolved;
            EndTime = DateTime.UtcNow;
        }

        public void ResetToHealthy()
        {
            Status = PartitionStatus.Healthy;
            EndTime = null;
        }
    }
}
