namespace CarAuction.Domain.Models.Partitions
{
    public enum PartitionStatus
    {
        Healthy,
        Partitioned,
        Reconciling,
        Resolved
    }
}
