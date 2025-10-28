namespace CarAuction.Domain.Models.Vehicles
{
    public class Sedan : Vehicle
    {
        public int NumberOfDoors { get; private set; }
        public bool HasSunroof { get; private set; }
        public double CargoCapacity { get; private set; }

        public Sedan(string brand, string model, int year, Region region, int numberOfDoors, bool hasSunroof, double cargoCapacity)
            : base(brand, model, year, region, VehicleType.Sedan)
        {
            NumberOfDoors = numberOfDoors;
            HasSunroof = hasSunroof;
            CargoCapacity = cargoCapacity;
        }

        private Sedan() : base() { }

        public void UpdateSedanDetails(int numberOfDoors, bool hasSunroof, double cargoCapacity)
        {
            NumberOfDoors = numberOfDoors;
            HasSunroof = hasSunroof;
            CargoCapacity = cargoCapacity;
        }
    }
}
