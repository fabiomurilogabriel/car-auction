using CarAuction.Domain.Models.Auctions;
using CarAuction.Domain.Models.Bids;
using CarAuction.Domain.Models.Partitions;
using CarAuction.Domain.Models.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace CarAuction.Infrastructure.Data
{
    public class AuctionDbContext(DbContextOptions<AuctionDbContext> options) : DbContext(options)
    {
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<Auction> Auctions { get; set; }
        public DbSet<Bid> Bids { get; set; }
        public DbSet<PartitionEvent> PartitionEvents { get; set; }
        public DbSet<BidSequence> BidSequences { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Vehicle hierarchy
            modelBuilder.Entity<Vehicle>()
                .HasDiscriminator<VehicleType>("Type")
                .HasValue<Sedan>(VehicleType.Sedan)
                .HasValue<SUV>(VehicleType.SUV)
                .HasValue<Hatchback>(VehicleType.Hatchback)
                .HasValue<Truck>(VehicleType.Truck);

            modelBuilder.Entity<Vehicle>()
                .Property(v => v.Region)
                .HasConversion<string>();

            modelBuilder.Entity<Vehicle>()
                .HasIndex(v => v.Region);

            // Auction
            modelBuilder.Entity<Auction>()
                .Property(a => a.Region)
                .HasConversion<string>();

            modelBuilder.Entity<Auction>()
                .Property(a => a.State)
                .HasConversion<string>();

            modelBuilder.Entity<Auction>()
                .HasIndex(a => new { a.Region, a.State });

            modelBuilder.Entity<Auction>()
                .HasOne(a => a.Vehicle)
                .WithMany()
                .HasForeignKey(a => a.VehicleId);

            modelBuilder.Entity<Auction>()
                .HasMany<Bid>()
                .WithOne()
                .HasForeignKey(b => b.AuctionId);

            modelBuilder.Entity<Auction>()
                .Property(a => a.Version)
                .IsConcurrencyToken();

            // Bid
            modelBuilder.Entity<Bid>()
                .Property(b => b.OriginRegion)
                .HasConversion<string>();

            modelBuilder.Entity<Bid>()
                .Property(b => b.RejectionReason)
                .IsRequired(false);

            modelBuilder.Entity<Bid>()
                .HasIndex(b => new { b.AuctionId, b.Sequence });

            modelBuilder.Entity<Bid>()
                .HasIndex(b => new { b.AuctionId, b.IsDuringPartition });

            // PartitionEvent
            modelBuilder.Entity<PartitionEvent>()
                .Property(p => p.OriginBidRegion)
                .HasConversion<string>();

            modelBuilder.Entity<PartitionEvent>()
                .Property(p => p.AuctionRegion)
                .HasConversion<string>();

            modelBuilder.Entity<PartitionEvent>()
                .Property(p => p.Status)
                .HasConversion<string>();

            modelBuilder.Entity<PartitionEvent>()
                .HasIndex(p => p.Status);

            // BidSequence
            modelBuilder.Entity<BidSequence>()
                .HasKey(bs => bs.AuctionId);
        }
    }
}
