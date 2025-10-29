using CarAuction.Domain.Abstractions.Repositories;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace CarAuction.Infrastructure.Data.Repositories
{
    public class VehicleRepository(AuctionDbContext context) : IVehicleRepository
    {
        private readonly AuctionDbContext _context = context;

        public async Task<Vehicle> GetByIdAsync(Guid id)
            => await _context.Vehicles.FindAsync(id);

        public async Task<IEnumerable<Vehicle>> GetByRegionAsync(Region region)
            => await _context.Vehicles
                .Where(v => v.Region == region)
                .ToListAsync();

        public async Task<Guid> CreateAsync(Vehicle vehicle)
        {
            _context.Vehicles.Add(vehicle);

            await _context.SaveChangesAsync();

            return vehicle.Id;
        }

        public async Task UpdateAsync(Vehicle vehicle)
        {
            _context.Vehicles.Update(vehicle);

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var vehicle = await GetByIdAsync(id);

            if (vehicle is not null)
            {
                _context.Vehicles.Remove(vehicle);

                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Vehicle>> GetAllAsync()
            => await _context.Vehicles.ToListAsync();
    }
}
