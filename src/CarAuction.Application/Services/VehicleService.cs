using CarAuction.Domain.Abstractions;
using CarAuction.Domain.Abstractions.Repositories;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Vehicles;

namespace CarAuction.Application.Services
{
    public class VehicleService(IVehicleRepository repository) : IVehicleService
    {
        private readonly IVehicleRepository _repository = repository;

        public async Task<Guid> CreateVehicleAsync(Vehicle vehicle)
        {

            try
            {
                if (vehicle is null)
                {
                    Console.Error.WriteLine("Attempted to create a vehicle with null reference");

                    throw new ArgumentNullException(nameof(vehicle));
                }

                return await _repository.CreateAsync(vehicle);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error creating vehicle: {ex.Message}");
                throw;
            }
        }

        public async Task<Vehicle> GetVehicleAsync(Guid id)
        {
            try
            {
                var vehicle = await _repository.GetByIdAsync(id);

                return vehicle ?? throw new KeyNotFoundException($"Vehicle with ID {id} not found");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error retrieving vehicle: {ex.Message}");
                throw;
            }
        }

        public async Task<IEnumerable<Vehicle>> GetVehiclesByRegionAsync(Region region)
        {
            try
            {
                return await _repository.GetByRegionAsync(region);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error retrieving vehicles by region: {ex.Message}");
                throw;
            }
        }

        public async Task<IEnumerable<Vehicle>> GetAllVehiclesAsync()
        {
            try
            {
                return await _repository.GetAllAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error retrieving all vehicles: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateVehicleAsync(Vehicle vehicle)
        {
            try
            {
                if (vehicle is null)
                {
                    Console.Error.WriteLine("Attempted to create a vehicle with null reference");

                    throw new ArgumentNullException(nameof(vehicle));
                }

                await _repository.UpdateAsync(vehicle);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error updating vehicle: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteVehicleAsync(Guid id)
        {
            try
            {
                await _repository.DeleteAsync(id);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error deleting vehicle: {ex.Message}");
                throw;
            }
        }
    }
}
