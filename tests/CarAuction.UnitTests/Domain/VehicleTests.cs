using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Vehicles;

namespace CarAuction.UnitTests.Domain
{
    public class VehicleTests
    {
        [Fact]
        public void Hatchback_ShouldCreateWithCorrectProperties()
        {
            // Act
            var hatchback = new Hatchback("VW", "Golf", 2024, Region.EUWest, 3, false, 380.5);

            // Assert
            Assert.Equal("VW", hatchback.Brand);
            Assert.Equal("Golf", hatchback.Model);
            Assert.Equal(2024, hatchback.Year);
            Assert.Equal(Region.EUWest, hatchback.Region);
            Assert.Equal(380.5, hatchback.CargoCapacity);
            Assert.Equal(VehicleType.Hatchback, hatchback.Type);
            Assert.Equal(3, hatchback.NumberOfDoors);
            Assert.False(hatchback.HasSunroof);
        }

        [Fact]
        public void Hatchback_UpdateHatchbackDetails_ShouldUpdateValues()
        {
            // Arrange
            var hatchback = new Hatchback("VW", "Golf", 2024, Region.EUWest, 3, true, 380.5);

            // Act
            hatchback.UpdateHatchbackDetails(5, false, 400.0);

            // Assert
            Assert.Equal(400.0, hatchback.CargoCapacity);
            Assert.Equal(5, hatchback.NumberOfDoors);
            Assert.False(hatchback.HasSunroof);
        }

        [Fact]
        public void Truck_ShouldCreateWithCorrectProperties()
        {
            // Act
            var truck = new Truck("Ford", "F-150", 2023, Region.USEast, "6.5ft", "Crew");

            // Assert
            Assert.Equal("Ford", truck.Brand);
            Assert.Equal("F-150", truck.Model);
            Assert.Equal(2023, truck.Year);
            Assert.Equal(Region.USEast, truck.Region);
            Assert.Equal("6.5ft", truck.BedSize);
            Assert.Equal("Crew", truck.CabType);
            Assert.Equal(VehicleType.Truck, truck.Type);
        }

        [Fact]
        public void Truck_UpdateTruckDetails_ShouldUpdateValues()
        {
            // Arrange
            var truck = new Truck("Ford", "F-150", 2023, Region.USEast, "6.5ft", "Crew");

            // Act
            truck.UpdateTruckDetails("8ft", "SuperCrew");

            // Assert
            Assert.Equal("8ft", truck.BedSize);
            Assert.Equal("SuperCrew", truck.CabType);
        }

        [Fact]
        public void Sedan_UpdateDetails_ShouldUpdateMakeModelYear()
        {
            // Arrange
            var sedan = new Sedan("Toyota", "Camry", 2023, Region.USEast, 4, true, 11);

            // Act
            sedan.UpdateDetails("Honda", "Accord", 2024);

            // Assert
            Assert.Equal("Honda", sedan.Brand);
            Assert.Equal("Accord", sedan.Model);
            Assert.Equal(2024, sedan.Year);
        }

        [Fact]
        public void Sedan_UpdateBrand_ShouldUpdateBrand()
        {
            // Arrange
            var sedan = new Hatchback("Toyota", "i20", 2023, Region.USEast, 4, true, 12);

            // Act
            sedan.UpdateBrand("Hyundai");

            // Assert
            Assert.Equal("Hyundai", sedan.Brand);
        }

        [Fact]
        public void Sedan_UpdateModel_ShouldUpdateModel()
        {
            // Arrange
            var sedan = new Hatchback("Hyundai", "i20", 2023, Region.USEast, 4, true, 12);

            // Act
            sedan.UpdateModel("i30");

            // Assert
            Assert.Equal("i30", sedan.Model);
        }

        [Fact]
        public void Sedan_UpdateYear_ShouldUpdateYear()
        {
            // Arrange
            var sedan = new Hatchback("Hyundai", "i20", 2023, Region.USEast, 4, true, 12);

            // Act
            sedan.UpdateYear(2025);

            // Assert
            Assert.Equal(2025, sedan.Year);
        }

        [Fact]
        public void Sedan_UpdateRegion_ShouldUpdateRegion()
        {
            // Arrange
            var sedan = new Hatchback("Hyundai", "i20", 2023, Region.USEast, 4, true, 12);

            // Act
            sedan.UpdateRegion(Region.EUWest);

            // Assert
            Assert.Equal(Region.EUWest, sedan.Region);
        }

        [Fact]
        public void Sedan_UpdateSedanDetails_ShouldUpdateSedanProperties()
        {
            // Arrange
            var sedan = new Sedan("Toyota", "Camry", 2023, Region.USEast, 4, true, 200.5);

            // Act
            sedan.UpdateSedanDetails(2, false, 430);

            // Assert
            Assert.Equal(2, sedan.NumberOfDoors);
            Assert.False(sedan.HasSunroof);
            Assert.Equal(430, sedan.CargoCapacity);
        }

        [Fact]
        public void SUV_UpdateSUVDetails_ShouldUpdateSUVProperties()
        {
            // Arrange
            var suv = new SUV("Ford", "Explorer", 2023, Region.USEast, true, true, 3, false, 550.1);

            // Act
            suv.UpdateSUVDetails(false, false, 5, true, 200.6);

            // Assert
            Assert.False(suv.HasThirdRow);
            Assert.False(suv.HasAllWheelDrive);
            Assert.Equal(5, suv.NumberOfDoors);
            Assert.True(suv.HasSunroof);
            Assert.Equal(200.6, suv.CargoCapacity);
        }

        [Fact]
        public void Vehicle_ChangeRegion_ShouldUpdateRegion()
        {
            // Arrange
            var vehicle = new Sedan("Toyota", "Camry", 2023, Region.USEast, 4, true, 18.1);

            // Act
            vehicle.UpdateRegion(Region.EUWest);

            // Assert
            Assert.Equal(Region.EUWest, vehicle.Region);
        }
    }
}
