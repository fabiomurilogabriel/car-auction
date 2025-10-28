using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Vehicles;

namespace CarAuction.Domain.Abstractions.Repositories
{
    public interface IVehicleRepository
    {
        Task<Vehicle> GetByIdAsync(Guid id);
        Task<IEnumerable<Vehicle>> GetByRegionAsync(Region region);
        Task<Guid> AddAsync(Vehicle vehicle);
        Task UpdateAsync(Vehicle vehicle);
        Task DeleteAsync(Guid id);
        Task<IEnumerable<Vehicle>> GetAllAsync();
    }
}
