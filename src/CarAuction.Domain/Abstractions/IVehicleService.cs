using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Vehicles;

namespace CarAuction.Domain.Abstractions
{
    public interface IVehicleService
    {
        Task<Guid> CreateVehicleAsync(Vehicle vehicle);
        Task<Vehicle> GetVehicleAsync(Guid id);
        Task<IEnumerable<Vehicle>> GetVehiclesByRegionAsync(Region region);

        Task<IEnumerable<Vehicle>> GetAllVehiclesAsync();
        Task UpdateVehicleAsync(Vehicle vehicle);
        Task DeleteVehicleAsync(Guid id);
    }
}
