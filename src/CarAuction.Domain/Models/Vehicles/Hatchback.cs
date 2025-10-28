namespace CarAuction.Domain.Models.Vehicles
{
    public class Hatchback : Vehicle
    {
        public int NumberOfDoors { get; private set; }
        public bool HasSunroof { get; private set; }
        public double CargoCapacity { get; private set; }

        public Hatchback(string brand, string model, int year, Region region, int numberOfDoors, bool hasSunroof, double cargoCapacity)
            : base(brand, model, year, region, VehicleType.Hatchback)
        {
            NumberOfDoors = numberOfDoors;
            HasSunroof = hasSunroof;
            CargoCapacity = cargoCapacity;
        }

        private Hatchback() : base() { }

        public void UpdateHatchbackDetails(int numberOfDoors, bool hasSunroof, double cargoCapacity)
        {
            NumberOfDoors = numberOfDoors;
            HasSunroof = hasSunroof;
            CargoCapacity = cargoCapacity;
        }
    }
}
