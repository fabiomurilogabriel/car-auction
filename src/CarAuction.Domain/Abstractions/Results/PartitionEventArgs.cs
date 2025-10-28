using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Partitions;

namespace CarAuction.Domain.Abstractions.Results
{
    public class PartitionEventArgs : EventArgs
    {
        public Region OriginBidRegion { get; set; }
        public Region AuctionRegion { get; set; }
        public PartitionStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsSolved { get; set; }
    }
}
