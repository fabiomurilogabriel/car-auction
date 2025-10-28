namespace CarAuction.Domain.Models.Vehicles
{
    public class SUV : Vehicle
    {
        public bool HasThirdRow { get; private set; }
        public bool HasAllWheelDrive { get; private set; }
        public int NumberOfDoors { get; private set; }
        public bool HasSunroof { get; private set; }
        public double CargoCapacity { get; private set; }

        public SUV(string brand, string model, int year, Region region, bool hasThirdRow, bool hasAllWheelDrive, int numberOfDoors, bool hasHasSunroof, double cargoCapacity)
            : base(brand, model, year, region, VehicleType.SUV)
        {
            HasThirdRow = hasThirdRow;
            HasAllWheelDrive = hasAllWheelDrive;
            NumberOfDoors = numberOfDoors;
            HasSunroof = hasHasSunroof;
            CargoCapacity = cargoCapacity;
        }

        private SUV() : base() { }

        public void UpdateSUVDetails(bool hasThirdRow, bool hasAllWheelDrive, int numberOfDoors, bool hasHasSunroof, double cargoCapacity)
        {
            HasThirdRow = hasThirdRow;
            HasAllWheelDrive = hasAllWheelDrive;
            NumberOfDoors = numberOfDoors;
            HasSunroof = hasHasSunroof;
            CargoCapacity = cargoCapacity;
        }
    }
}
