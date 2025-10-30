# Architecture

The system runs in two regions (US-East and EU-West) that can communicate with each other, but sometimes the network connection fails. When that happens, each region keeps working independently and they sync up later.

## Database Schema

Five main tables:
- **Vehicles** - Cars, trucks, etc. with all types in one table
- **Auctions** - Active auctions with current price and version for conflict detection
- **Bids** - All bids with sequence numbers and partition flags
- **BidSequences** - Atomic counters to guarantee bid ordering
- **PartitionEvents** - Log of network partition events

Key indexes for performance:
- Auctions by region and state
- Bids ordered by sequence number
- Partition bids for reconciliation

## Main Components

**Domain Models**
- Auction - Has states (Draft → Active → Paused → Ended) and version control
- Bid - Has sequence numbers and knows which region it came from
- Vehicle - Base class for different vehicle types

**Services**
- AuctionService - Main business logic
- RegionCoordinator - Detects network partitions
- ConflictResolver - Handles bid conflicts after partitions

## How Bids Work

When someone places a bid:
1. Check if it's a local bid (same region) or cross-region
2. If local and no partition - process immediately (strong consistency)
3. If cross-region during partition - queue for later (availability priority)
4. Otherwise reject the bid

## Data Access

Using Entity Framework with repositories. The auction updates use optimistic locking - if two regions try to update the same auction at the same time, one will fail and we'll resolve the conflict.

## Consistency Strategies

**Strong Consistency (Local Bids)**
Everything happens in a database transaction - get sequence number, validate bid, update auction price. If anything fails, roll it all back.

**Eventual Consistency (Cross-Region During Partition)**
Just save the bid with a "during partition" flag and process it later when the partition heals.

## Reconciliation After Partition

When the partition heals:
1. Get all bids (normal ones + queued ones)
2. Sort them by timestamp and sequence number
3. Resolve conflicts (highest bid wins)
4. Update the auction with the final winner
5. Resume the auction or end it if time expired

**Conflict Resolution Rules:**
- Higher bid amount wins
- If tied, earlier timestamp wins
- Only bids placed before auction deadline count

## Event Handling

When a partition is detected, the system automatically pauses auctions that would be affected. When the partition heals, it triggers reconciliation for any auctions that had queued bids.

## Performance Targets

- Bid processing under 200ms (95th percentile)
- Support 1000+ concurrent auctions per region
- Handle 10,000 concurrent users per region
- Maintain 99.9% availability even during network issues
- Zero bid loss during partitions

## Challenge Requirements Met

**Main Scenario**: 5-minute network partition between regions
- EU user bids on US auction → Gets queued
- US user bids on same auction → Processes immediately
- Auction ending during partition → Waits for reconciliation
- After partition heals → All bids processed fairly

**CAP Theorem Trade-offs**:
- Create auction: Strong consistency (avoid duplicates)
- Local bids: Strong consistency (immediate processing)
- Cross-region bids: Availability (queue during partitions)
- View auction: Configurable based on use case

## Testing

**Unit Tests** - Individual components work correctly
**Integration Tests** - Full scenarios including the main 5-minute partition challenge
**Performance Tests** - System handles required load

Run the main test: `dotnet test --filter "ExactChallengeScenario_5MinutePartition_ShouldMeetAllRequirements"`

## Summary

This system handles network partitions gracefully by making smart trade-offs between consistency and availability. Local operations stay fast and consistent, while cross-region operations prioritize availability during network issues. After partitions heal, everything gets reconciled automatically with no data loss.