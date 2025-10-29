-- =====================================================
-- Distributed Car Auction Platform - Database Schema
-- =====================================================
-- Description: Complete schema for distributed auction system
-- Features: Vehicle inheritance, Optimistic locking, Partition tracking
-- Regions: US-East, EU-West
-- =====================================================

-- ============================================
-- 1. VEHICLES TABLE (with TPH inheritance)
-- ============================================
CREATE TABLE Vehicles (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Brand NVARCHAR(100) NOT NULL,
    Model NVARCHAR(100) NOT NULL,
    Year INT NOT NULL,
    VehicleType NVARCHAR(20) NOT NULL,
    Region NVARCHAR(20) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL DEFAULT GETUTCDATE(),
    NumberOfDoors INT NULL,
    HasSunroof BIT NULL,
    HasThirdRow BIT NULL,
    HasAllWheelDrive BIT NULL,
    CargoCapacity FLOAT NULL,
    BedSize NVARCHAR(50) NULL,
    CabType NVARCHAR(50) NULL,

    INDEX IX_Vehicles_Region (Region),
    INDEX IX_Vehicles_VehicleType (VehicleType),
    INDEX IX_Vehicles_Region_VehicleType (Region, VehicleType)
);

-- ============================================
-- 2. AUCTIONS TABLE
-- ============================================
CREATE TABLE Auctions (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    VehicleId UNIQUEIDENTIFIER NOT NULL,
    Region NVARCHAR(20) NOT NULL,
    State NVARCHAR(20) NOT NULL,
    StartingPrice DECIMAL(18,2) NOT NULL,
    ReservePrice DECIMAL(18,2) NULL,
    CurrentPrice DECIMAL(18,2) NOT NULL,
    WinningBidderId UNIQUEIDENTIFIER NULL,
    StartTime DATETIME2 NOT NULL,
    EndTime DATETIME2 NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Version BIGINT NOT NULL DEFAULT 0, 

    CONSTRAINT FK_Auctions_Vehicles FOREIGN KEY (VehicleId) 
        REFERENCES Vehicles(Id) ON DELETE CASCADE,
    
    INDEX IX_Auctions_Region_State (Region, State),
    INDEX IX_Auctions_State (State),
    INDEX IX_Auctions_EndTime (EndTime),
    INDEX IX_Auctions_VehicleId (VehicleId)
);

-- ============================================
-- 3. BIDS TABLE
-- ============================================
CREATE TABLE Bids (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    AuctionId UNIQUEIDENTIFIER NOT NULL,
    BidderId UNIQUEIDENTIFIER NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Sequence BIGINT NOT NULL,
    OriginRegion NVARCHAR(20) NOT NULL,
    IsAccepted BIT NOT NULL DEFAULT 0,
    RejectionReason NVARCHAR(500) NULL,
    IsDuringPartition BIT NOT NULL DEFAULT 0, 

    CONSTRAINT FK_Bids_Auctions FOREIGN KEY (AuctionId) 
        REFERENCES Auctions(Id) ON DELETE CASCADE,
    
    INDEX IX_Bids_AuctionId_Sequence (AuctionId, Sequence),
    INDEX IX_Bids_AuctionId_CreatedAt (AuctionId, CreatedAt),
    INDEX IX_Bids_Partition (AuctionId, IsDuringPartition),
    INDEX IX_Bids_BidderId (BidderId)
);

-- ============================================
-- 4. BID SEQUENCES TABLE
-- ============================================
CREATE TABLE BidSequences (
    AuctionId UNIQUEIDENTIFIER PRIMARY KEY,
    CurrentSequence BIGINT NOT NULL DEFAULT 0,
    LastUpdated DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_BidSequences_Auctions FOREIGN KEY (AuctionId) 
        REFERENCES Auctions(Id) ON DELETE CASCADE
);

-- ============================================
-- 5. PARTITION EVENTS TABLE
-- ============================================
CREATE TABLE PartitionEvents (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    OriginBidRegion NVARCHAR(20) NOT NULL,
    AuctionRegion NVARCHAR(20) NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    EndTime DATETIME2 NULL,
    
    INDEX IX_PartitionEvents_Status (Status),
    INDEX IX_PartitionEvents_AuctionRegion (AuctionRegion),
    INDEX IX_PartitionEvents_Status_AuctionRegion (Status, AuctionRegion)
);

-- ============================================
-- CONSTRAINTS AND CHECKS
-- ============================================

-- Ensure valid vehicle types
ALTER TABLE Vehicles ADD CONSTRAINT CHK_Vehicles_VehicleType 
    CHECK (VehicleType IN ('Sedan', 'SUV', 'Hatchback', 'Truck'));

-- Ensure valid regions
ALTER TABLE Vehicles ADD CONSTRAINT CHK_Vehicles_Region 
    CHECK (Region IN ('USEast', 'EUWest'));

ALTER TABLE Auctions ADD CONSTRAINT CHK_Auctions_Region 
    CHECK (Region IN ('USEast', 'EUWest'));

ALTER TABLE Bids ADD CONSTRAINT CHK_Bids_Region 
    CHECK (OriginRegion IN ('USEast', 'EUWest'));

-- Ensure valid auction states
ALTER TABLE Auctions ADD CONSTRAINT CHK_Auctions_State 
    CHECK (State IN ('Draft', 'Active', 'Paused', 'Ended', 'Cancelled'));

-- Ensure valid partition statuses
ALTER TABLE PartitionEvents ADD CONSTRAINT CHK_PartitionEvents_Status 
    CHECK (Status IN ('Healthy', 'Partitioned', 'Reconciling', 'Resolved'));

-- Ensure positive prices
ALTER TABLE Auctions ADD CONSTRAINT CHK_Auctions_StartingPrice 
    CHECK (StartingPrice > 0);

ALTER TABLE Auctions ADD CONSTRAINT CHK_Auctions_CurrentPrice 
    CHECK (CurrentPrice >= 0);

ALTER TABLE Bids ADD CONSTRAINT CHK_Bids_Amount 
    CHECK (Amount > 0);

-- Ensure valid time ranges
ALTER TABLE Auctions ADD CONSTRAINT CHK_Auctions_TimeRange 
    CHECK (EndTime > StartTime);

-- =====================================================
-- SAMPLE SEED DATA (Optional for testing)
-- =====================================================

-- Insert sample vehicles
INSERT INTO Vehicles (Id, Brand, Model, Year, VehicleType, Region, NumberOfDoors, HasSunroof)
VALUES 
    (NEWID(), 'Toyota', 'Camry', 2023, 'Sedan', 'USEast', 4, 1),
    (NEWID(), 'Honda', 'Accord', 2024, 'Sedan', 'EUWest', 4, 0);

INSERT INTO Vehicles (Id, Brand, Model, Year, VehicleType, Region, HasThirdRow, HasAllWheelDrive)
VALUES 
    (NEWID(), 'Ford', 'Explorer', 2023, 'SUV', 'USEast', 1, 1),
    (NEWID(), 'Mazda', 'CX-9', 2024, 'SUV', 'EUWest', 0, 1);

-- =====================================================
-- NOTES
-- =====================================================
-- 1. Table-Per-Hierarchy (TPH) is used for Vehicle inheritance
-- 2. Optimistic locking via Version column in Auctions table
-- 3. BidSequences ensures atomic sequence generation
-- 4. All timestamps use DATETIME2 for precision
-- 5. Indexes optimized for common query patterns:
--    - Region-based lookups
--    - Active auction queries
--    - Bid ordering by sequence
--    - Partition tracking
-- =====================================================
