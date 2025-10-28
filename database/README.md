# Database Design - Distributed Car Auction Platform

## Overview

This database schema supports a distributed auction system operating across two geographic regions (US-East and EU-West), with emphasis on handling network partitions and maintaining data consistency.

## Design Decisions

### 1. Vehicle Inheritance (Table-Per-Hierarchy)

We chose **TPH (Table-Per-Hierarchy)** instead of TPT (Table-Per-Type) for the following reasons:

- **Performance**: Single table queries are faster than joins across multiple tables
- **Simplicity**: All vehicle data in one location
- **Flexibility**: Easy to add new vehicle types without schema changes
- **Trade-off**: Some null columns, but acceptable given limited vehicle types (4 types)

The `Type` column acts as a discriminator to distinguish between Sedan, SUV, Hatchback, and Truck.

### 2. Optimistic Locking

The `Version` column in the Auctions table enables optimistic concurrency control:

- Prevents lost updates when multiple regions update the same auction
- Lightweight compared to pessimistic locking
- Essential for distributed systems with network partitions

**How it works:**
UPDATE Auctions
SET CurrentPrice = @newPrice, Version = Version + 1
WHERE Id = @auctionId AND Version = @expectedVersion

If the version doesn't match, the update fails and conflict resolution kicks in.

### 3. Bid Ordering Strategy

**Problem**: Cross-region bids need guaranteed ordering.

**Solution**: Combination of `Sequence` and `Timestamp`:
- `Sequence`: Atomic counter per auction (via BidSequences table)
- `Timestamp`: Fallback for tie-breaking during reconciliation
- `IsDuringPartition`: Flags bids placed during network partition

### 4. Partition Tracking

The `PartitionEvents` table provides:
- Historical record of all network partitions
- Current partition status for coordination
- Duration tracking for SLA monitoring

## Indexes Strategy

### High-frequency queries optimized:

1. **Active auctions by region**
INDEX IX_Auctions_Region_State (Region, State)

2. **Bid ordering per auction**
INDEX IX_Bids_AuctionId_Sequence (AuctionId, Sequence)

3. **Partition bids lookup**
INDEX IX_Bids_Partition (AuctionId, IsDuringPartition)

## CAP Theorem Trade-offs

This design prioritizes **AP (Availability + Partition Tolerance)** with eventual consistency:

- **During partition**: Each region accepts bids independently
- **After partition**: Reconciliation process resolves conflicts using timestamp + sequence
- **Consistency level**: Configurable (strong vs eventual) via application layer

## Transaction Boundaries

### Critical transactions requiring ACID:

1. **Placing a bid**:
BEGIN TRANSACTION
- Get next sequence (BidSequences)
- Insert bid (Bids)
- Update auction price (Auctions with version check)
COMMIT


2. **Creating auction**:
BEGIN TRANSACTION
- Insert auction (Auctions)
- Initialize bid sequence (BidSequences)
COMMIT

text

3. **Reconciliation** (post-partition):
BEGIN TRANSACTION
- Get all partition bids
- Determine winner (conflict resolution)
- Update auction final state
- Mark bids as accepted/rejected
COMMIT

text

## Schema Evolution

Future considerations:

- **Audit log table**: Track all state changes
- **Read replicas**: Region-specific read-only copies
- **Sharding strategy**: Partition by Region column if scale requires

## Performance Estimates

Based on requirements (1000+ concurrent auctions, 10K users per region):

- **Expected bid rate**: ~500 bids/second peak
- **Storage growth**: ~1GB/month with typical auction volume
- **Query performance**: <50ms for indexed lookups

## Setup Instructions

### SQL Server:
sqlcmd -S localhost -d AuctionDB -i Schema.sql

text

### EF Core In-Memory (for tests):
var options = new DbContextOptionsBuilder<AuctionDbContext>()
.UseInMemoryDatabase("AuctionTest")
.Options;

text

---

**Related Challenge Requirements**: Database Design, Transaction boundaries, CAP theorem implementation