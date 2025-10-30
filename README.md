# Car Auction Platform

A distributed auction system built to handle network partitions gracefully. The system operates across US-East and EU-West regions, making smart trade-offs between consistency and availability based on the CAP theorem.

## Getting Started

You'll need .NET 8.0 SDK installed. The tests use in-memory databases, so no SQL Server setup required.

```bash
# Run all tests
dotnet test

# Run the main challenge scenario
dotnet test --filter "ExactChallengeScenario_5MinutePartition_ShouldMeetAllRequirements"
```

### Project Structure

```
CarAuction/
├── .github/
│   └── workflows/
│       ├── ci.yml                   # GitHub Actions CI/CD pipeline
├── src/
│   ├── CarAuction.Domain/           # Domain models and abstractions
│   ├── CarAuction.Application/      # Application services and business logic
│   └── CarAuction.Infrastructure/   # Repository implementations and simulators
├── tests/
│   ├── CarAuction.UnitTests/        # Unit tests
│   └── CarAuction.IntegrationTests/ # Integration tests and distributed scenarios
├── database/
│   ├── Schema.sql                   # Complete database schema
│   └── README.md                    # Database design documentation
├── Solution Items/
│   ├── ARCHITECTURE.md              # Technical architecture documentation
│   └── TESTS_SUMMARY.md             # Comprehensive test coverage summary
├── coverage-report/                 # Test coverage reports
├── README.md                        # This file - project overview
└── CarAuction.sln                   # Solution file
```

## How It Works

The system has three main parts:

**Domain** - Vehicles, Auctions, and Bids with proper state management and abstractions as well

**Application** - Handle auction logic, region coordination, and conflict resolution  

**Infractruture Layer** - Repositories with Entity Framework and optimized queries, plus simulators for network partitions and multi-region behavior

## Domain Decisions

Some key design choices that make the system work better:

**Vehicle Types** - You can't create a generic "Vehicle" - it has to be a specific type like Sedan, SUV, Hatchback, or Truck. This prevents mistakes and ensures every vehicle has the right properties.

**Auction States** - Auctions follow a clear path: Draft → Active → Paused → Ended. Once an auction moves to the next state, it can't go backwards (except pausing during network issues).

**Bid Ordering** - Every bid gets a sequence number (1, 2, 3...) so we always know the exact order, even when bids come from different regions at nearly the same time.

**Region Tracking** - Each bid remembers which region it came from. This helps us make the right consistency decisions and resolve conflicts fairly.

**Immutable Bids** - Once a bid is placed, its core details (amount, bidder, timestamp) can't be changed. We can only mark it as accepted/rejected during conflict resolution.

**Bid Winner Selection** - When multiple bids compete (especially after network partitions), we use a clear priority system: highest amount wins first, then earliest timestamp if tied, then lowest sequence number as final tiebreaker. This ensures the same winner every time, no matter which region processes the reconciliation.

## CAP Theorem Decisions

Different operations make different trade-offs based on what matters most:

**Creating Auctions (CP - Strong Consistency)**
We chose consistency over availability because duplicate auctions would be a disaster. If someone tries to create an auction and the other region is unreachable, we'd rather fail the request than risk having two auctions for the same vehicle. Better safe than sorry.

**Placing Bids - It Depends**
- *Local bids (same region):* Strong consistency (CP) - these get processed immediately, but if we have a partition, we might reject them
- *Cross-region bids:* Availability (AP) - during network partitions, we queue these bids and process them later. Users get a "bid received" confirmation, and we sort everything out when the network heals

**Ending Auctions (CP - Strong Consistency)**
When an auction is supposed to end during a network partition, we pause it instead. We won't declare a winner until we can reconcile all the queued bids from both regions. This ensures the real highest bidder always wins.

**Viewing Auction Status (Configurable)**
- *For placing bids:* Strong consistency - you need current data to make bidding decisions
- *For browsing:* Eventual consistency - it's fine if the price is a few seconds behind when you're just looking around

## CAP Decisions Summary

| Operation | CAP Choice | Justification |
|----------|-------------|---------------|
| **Create Auction** | **CP** | Strong consistency to avoid duplicates |
| **Local Bid** | **CP** | Strong consistency within region |
| **Cross-Region Bid** | **AP** | Availability during partitions |
| **View Auction** | **Configurable** | Strong/Eventual based on context |
| **End Auction** | **CP** | Final result integrity |

## Network Partition Handling

When regions can't communicate:
- Local bids continue working normally
- Cross-region bids get queued for later
- Auctions pause if they're supposed to end during the partition

After the partition heals:
- All queued bids are processed in order
- Conflicts are resolved deterministically
- No bids are ever lost

## The Challenge Scenario

The main test simulates a 5-minute network partition between US-East and EU-West:

- EU user bids on US auction → Gets queued
- US user bids on same auction → Processes immediately  
- Auction ending during partition → Waits for reconciliation
- After partition heals → All bids processed, winner determined fairly

## Database

Using Entity Framework with:
- Table-per-hierarchy for different vehicle types
- Optimistic locking to handle concurrent updates
- Sequence numbers to guarantee bid ordering
- Indexes optimized for the most common queries

## More Details

- **[Architecture](https://github.com/fabiomurilogabriel/car-auction/blob/main/Solution%20Items/ARCHITECTURE.md)** - Technical deep dive
- **[Tests](https://github.com/fabiomurilogabriel/car-auction/blob/main/Solution%20Items/TESTS_SUMMARY.md)** - Test coverage summary
- **[Database](https://github.com/fabiomurilogabriel/car-auction/tree/main/database)** - Schema and design decisions
