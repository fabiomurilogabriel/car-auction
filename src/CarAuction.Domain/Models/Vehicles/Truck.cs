namespace CarAuction.Domain.Models.Vehicles
{
    public class Truck : Vehicle
    {
        public string BedSize { get; private set; }
        public string CabType { get; private set; }

        public Truck(string brand, string model, int year, Region region, string bedSize, string cabType)
            : base(brand, model, year, region, VehicleType.Truck)
        {
            BedSize = bedSize;
            CabType = cabType;
        }

        private Truck() : base() { }

        public void UpdateTruckDetails(string bedSize, string cabType)
        {
            BedSize = bedSize;
            CabType = cabType;
        }
    }
}
