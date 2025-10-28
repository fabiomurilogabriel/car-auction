namespace CarAuction.Domain.Models.Vehicles
{
    public abstract class Vehicle
    {
        public Guid Id { get; private set; }
        public string Brand { get; private set; }
        public string Model { get; private set; }
        public int Year { get; private set; }
        public VehicleType Type { get; private set; }
        public Region Region { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        protected Vehicle(string brand, string model, int year, Region region, VehicleType type)
        {
            Id = Guid.NewGuid();
            Brand = brand;
            Model = model;
            Year = year;
            Region = region;
            Type = type;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = null;
        }

        protected Vehicle() { }

        public void UpdateDetails(string brand, string model, int year)
        {
            Brand = brand;
            Model = model;
            Year = year;
            UpdatedAt = null;
        }

        public void UpdateBrand(string brand)
        {
            Brand = brand;
            UpdatedAt = null;
        }

        public void UpdateModel(string model)
        {
            Model = model;
            UpdatedAt = null;
        }

        public void UpdateYear(int year)
        {
            Year = year;
            UpdatedAt = null;
        }

        public void UpdateRegion(Region region)
        {
            Region = region;
            UpdatedAt = null;
        }
    }
}
