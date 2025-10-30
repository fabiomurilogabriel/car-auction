# Database Design

The database is designed to handle auctions across two regions while dealing with network issues gracefully.

## Database Schema

```
┌─────────────────┐    ┌─────────────────┐    ┌──────────────┐
│    VEHICLES     │    │    AUCTIONS     │    │     BIDS     │
├─────────────────┤    ├─────────────────┤    ├──────────────┤
│ Id (PK)         │◄───┤ VehicleId (FK)  │◄───┤ AuctionId(FK)│
│ Brand           │    │ Id (PK)         │    │ Id (PK)      │
│ Model           │    │ Region          │    │ BidderId     │
│ Year            │    │ State           │    │ Amount       │
│ VehicleType     │    │ StartingPrice   │    │ Sequence     │
│ Region          │    │ CurrentPrice    │    │ OriginRegion │
│ CreatedAt       │    │ ReservePrice    │    │ CreatedAt    │
│ UpdatedAt       │    │ StartTime       │    │ IsAccepted   │
│                 │    │ EndTime         │    │ IsDuringPart │
│ -- TPH Fields --│    │ WinningBidderId │    │ RejectionRsn │
│ NumberOfDoors   │    │ Version         │    └──────────────┘
│ HasSunroof      │    │ CreatedAt       │
│ HasThirdRow     │    │ UpdatedAt       │
│ CargoCapacity   │    └─────────────────┘
│ BedSize         │             │
│ CabType         │             │
└─────────────────┘             │
                                │
┌─────────────────┐             │    ┌─────────────────┐
│  BID_SEQUENCES  │             │    │ PARTITION_EVENTS│
├─────────────────┤             │    ├─────────────────┤
│ AuctionId (PK)  │◄────────────┘    │ Id (PK)         │
│ CurrentSequence │                  │ OriginBidRegion │
│ LastUpdated     │                  │ AuctionRegion   │
└─────────────────┘                  │ Status          │
                                     │ CreatedAt       │
                                     │ EndTime         │
                                     └─────────────────┘
```

### Relationships:
- **Vehicles** → **Auctions** (1:N) - One vehicle can have multiple auctions
- **Auctions** → **Bids** (1:N) - One auction can have multiple bids
- **Auctions** → **BidSequences** (1:1) - Each auction has one sequence counter
- **PartitionEvents** - Standalone table tracking network partition history

## Design Decisions

### Vehicle Storage

All vehicle types (Sedan, SUV, Hatchback, Truck) go in one table with a `Type` column. This keeps queries simple and fast, even though some columns will be null for certain vehicle types.

### Handling Concurrent Updates

Each auction has a `Version` number that gets incremented on every update. This prevents two regions from overwriting each other's changes - if the version doesn't match what you expect, the update fails and we know there's a conflict to resolve.

### Bid Ordering

Each bid gets a sequence number (1, 2, 3...) per auction, plus a timestamp. This guarantees we can always figure out the correct order, even when bids come from different regions during network issues.

### Network Partition Tracking

We keep a log of when network partitions happen and how long they last. This helps with debugging and monitoring system health.

## Performance

Indexes are set up for the most common queries:
- Finding active auctions in a region
- Getting bids for an auction in order
- Finding bids that happened during partitions

## CAP Trade-offs

During network partitions, we choose availability over consistency - each region keeps working independently. After the partition heals, we reconcile everything to get back to a consistent state.

## Transactions

Three main operations need to be atomic:

1. **Placing a bid** - Get sequence number, save bid, update auction price
2. **Creating auction** - Create auction record and initialize bid counter
3. **Reconciliation** - Process all queued bids and determine final winner

## Setup

For production, you'd run the Schema.sql file against SQL Server. For tests, we use Entity Framework's in-memory database which is much simpler to set up.