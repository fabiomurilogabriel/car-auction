using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Auctions;
using CarAuction.Domain.Models.Bids;
using CarAuction.Domain.Models.Vehicles;

namespace CarAuction.IntegrationTests.Helpers
{
    public static class TestDataBuilder
    {
        public static Vehicle CreateTestVehicle(Region region = Region.USEast, VehicleType type = VehicleType.Sedan)
        {
            return type switch
            {
                VehicleType.SUV => new SUV("Ford", "Explorer", 2023, region, true, true, 3, false, 550.1),
                VehicleType.Hatchback => new Hatchback("VW", "Golf", 2024, region, 1, false, 380.5),
                VehicleType.Truck => new Truck("Ford", "F-150", 2023, region, "6.5ft", "Crew"),
                _ => new Sedan("Toyota", "Camry", 2023, region, 4, true, 12)
            };
        }

        public static Auction CreateTestAuction(Guid vehicleId, Region region = Region.USEast)
        {
            return new Auction(
                vehicleId,
                region,
                10000m,
                15000m,
                DateTime.UtcNow.AddHours(1),
                DateTime.UtcNow.AddHours(24)
            );
        }

        public static Auction CreateTestAuction(Guid vehicleId, Region region, DateTime startTime, DateTime endTime)
        {
            return new Auction(
                vehicleId,
                region,
                10000m,
                15000m,
                startTime,
                endTime
            );
        }

        public static Bid CreateTestBid(Guid auctionId, decimal amount, Region region = Region.USEast, long sequence = 1)
        {
            return new Bid(
                auctionId,
                Guid.NewGuid(), // BidderId
                amount,
                region,
                sequence
            );
        }
    }
}
