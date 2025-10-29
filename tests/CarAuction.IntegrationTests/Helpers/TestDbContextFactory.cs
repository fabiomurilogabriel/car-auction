using CarAuction.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CarAuction.IntegrationTests.Helpers
{
    public static class TestDbContextFactory
    {
        public static AuctionDbContext CreateInMemoryContext(string databaseName = null)
        {
            var dbName = databaseName ?? Guid.NewGuid().ToString();

            var options = new DbContextOptionsBuilder<AuctionDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var context = new AuctionDbContext(options);
            context.Database.EnsureCreated();

            return context;
        }
    }
}
