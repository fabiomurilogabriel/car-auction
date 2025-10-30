# Distributed Car Auction Platform - Senior Engineering Challenge

## Overview

This is a complete implementation of a distributed car auction system that operates in two geographical regions (US-East and EU-West), with special focus on network partition handling and CAP theorem trade-offs.

## How to Run

### Prerequisites
- .NET 8.0 SDK
- SQL Server (or InMemory for tests)

### Running Tests
```bash
# All tests
dotnet test

# Specific challenge scenario
dotnet test --filter "ExactChallengeScenario_5MinutePartition_ShouldMeetAllRequirements"

# With code coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML coverage report (optional)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"tests/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html
```

### Install XPlat Code Coverage
```bash
# Install coverage tool (if not already installed)
dotnet add package coverlet.collector

# Or install globally
dotnet tool install -g dotnet-coverage
```

### Database Setup
```bash
# SQL Server
sqlcmd -S localhost -d CarAuctionDB -i database/Schema.sql
```

## Solution Architecture

📋 **[Complete Technical Architecture →](ARCHITECTURE.md)**

🧪 **[Detailed Test Coverage →](TESTS_SUMMARY.md)**

🔬 **[Integration Tests Summary →](INTEGRATION_TESTS_SUMMARY.md)**

### Project Structure

```
CarAuction/
├── .github/
│   └── workflows/
│       ├── ci.yml                  # GitHub Actions CI/CD pipeline
├── src/
│   ├── CarAuction.Domain/          # Domain models and abstractions
│   ├── CarAuction.Application/     # Application services and business logic
│   └── CarAuction.Infrastructure/  # Repository implementations and simulators
├── tests/
│   ├── CarAuction.UnitTests/       # Unit tests
│   └── CarAuction.IntegrationTests/ # Integration tests and distributed scenarios
├── database/
│   ├── Schema.sql                  # Complete database schema
│   └── README.md                   # Database design documentation
├── Solution Items/
│   ├── ARCHITECTURE.md             # Technical architecture documentation
│   ├── TESTS_SUMMARY.md            # Comprehensive test coverage summary
│   └── INTEGRATION_TESTS_SUMMARY.md # Integration tests detailed summary
├── coverage-report/                # Test coverage reports
├── README.md                   # This file - project overview
└── CarAuction.sln                  # Solution file
```

### Main Components

#### 1. **Domain Models**
- **Vehicle**: Base class with TPH inheritance (Sedan, SUV, Hatchback, Truck)
- **Auction**: State machine with optimistic version control
- **Bid**: Bids with sequencing and origin tracking
- **PartitionEvent**: Partition event tracking

#### 2. **Distributed Services**
- **AuctionService**: Auction and bid management
- **RegionCoordinator**: Inter-region coordination and partition detection
- **BidOrderingService**: Bid ordering guarantees
- **ConflictResolver**: Post-partition conflict resolution

#### 3. **Data Layer**
- **Repositories**: Repository pattern with Entity Framework
- **AuctionDbContext**: Context with optimized configurations
- **BidSequence**: Atomic sequence generation

## Consistency Decisions (CAP Theorem)

### 1. Creating an Auction: **CP (Consistency + Partition Tolerance)**

**Decision:** Strong Consistency

**Why:**
- Avoiding duplicate auctions is critical for business integrity
- Vehicle validation must be atomic (one vehicle = one active auction)
- Creation failure is preferable to inconsistencies

### 2. Placing a Bid: **Hybrid (CP local, AP cross-region)**

**Local Bids (same region):** **CP**
- Strong consistency within the region
- ACID transactions with optimistic version control
- Immediate failure if conflict detected

**Cross-Region Bids:** **AP during partitions**
- Availability priority during network partitions
- Bids queued for later reconciliation
- User receives "bid queued" confirmation

### 3. Ending an Auction: **CP (Consistency + Partition Tolerance)**

**During Partitions:**
- Auctions are **paused** until complete reconciliation
- No auction ends with inconsistent data
- Final result integrity is guaranteed

**Post-Partition:**
- Complete reconciliation before determining winner
- All bids are ordered deterministically
- Final result is consistent across all regions

### 4. Viewing Auction Status: **Configurable (Strong vs Eventual)**

**Different levels based on context:**

**Strong Consistency (CP):**
- View for **placing final bid** (critical decision)
- View for **administrators** (auditing)
- Queries with `ConsistencyLevel.Strong`

**Eventual Consistency (AP):**
- View for general **browsing**
- Auction lists for **discovery**
- Queries with `ConsistencyLevel.Eventual`

## CAP Decisions Summary

| Operation | CAP Choice | Justification |
|----------|-------------|---------------|
| **Create Auction** | **CP** | Critical integrity - avoid duplicates |
| **Local Bid** | **CP** | Strong consistency within region |
| **Cross-Region Bid** | **AP** | Availability during partitions |
| **End Auction** | **CP** | Final result must be consistent |
| **View (Critical)** | **CP** | Important decisions need current data |
| **View (Browse)** | **AP** | User experience - always available |

### Partition Strategy

**During Partition:**
- ✅ Local bids continue normally (CP)
- ✅ Cross-region bids are queued (AP)
- ✅ Auctions scheduled to end are paused
- ✅ Partition events are logged for auditing

**Post-Partition (Reconciliation):**
- ✅ All bids are ordered by timestamp + sequence
- ✅ Conflicts resolved deterministically
- ✅ Final state consistent across regions
- ✅ Complete audit trail maintained
- ✅ No bids are lost

## Implemented Partition Scenario

### Specific Problem Solved:
```
Network Partition: Connection between US-East and EU-West is lost for 5 minutes

During partition:
✅ EU user tries bid on US auction → Queued for reconciliation
✅ US user bids on same US auction → Processed normally  
✅ Auction scheduled to end during partition → Paused until reconciliation

Post-partition:
✅ No bids are lost
✅ Winner determined deterministically
✅ Auction integrity maintained
```

## Database Design

### Main Features:
- **TPH (Table-Per-Hierarchy)** for vehicle inheritance
- **Optimistic version control** for auctions
- **Atomic sequencing** for bids
- **Optimized indexes** for distributed queries
- **Partition tracking** for auditing

## 📚 Documentation

- **[Technical Architecture](./Solution%20Items/ARCHITECTURE.md)** - Complete system design, database schema, and algorithms
- **[Test Summary](./Solution%20Items/TESTS_SUMMARY.md)** - Comprehensive test coverage and validation
- **[Integration Tests](./INTEGRATION_TESTS_SUMMARY.md)** - Detailed integration test scenarios
- **[Database Design](./database/README.md)** - Database schema and design decisions

## 🚀 Quick Links

- **Main Challenge Test**: `dotnet test --filter "ExactChallengeScenario_5MinutePartition_ShouldMeetAllRequirements"`
- **Performance Tests**: `dotnet test --filter "PerformanceAndConcurrencyTests"`
- **CAP Consistency Tests**: `dotnet test --filter "CAPConsistencyTests"`
