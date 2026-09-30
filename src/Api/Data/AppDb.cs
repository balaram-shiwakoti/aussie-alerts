using AussieAlerts.Domain;
using Microsoft.EntityFrameworkCore;

namespace AussieAlerts.Data;

public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<PropertyAlert> Alerts => Set<PropertyAlert>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(254);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Role).HasMaxLength(20);
        });
        b.Entity<Listing>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Suburb).HasMaxLength(80);
            e.Property(x => x.Postcode).HasMaxLength(4);
            e.Property(x => x.PropertyType).HasMaxLength(20);
            e.Property(x => x.Price).HasPrecision(14, 2);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("clock_timestamp()");
            e.HasIndex(x => new { x.Postcode, x.Price });
            e.HasIndex(x => x.Price);
            e.HasIndex(x => x.CreatedAt);
            e.ToTable(t => t.HasCheckConstraint("CK_Listings_Values",
                "\"Price\" >= 0 AND \"Bedrooms\" BETWEEN 0 AND 20"));
        });
        b.Entity<PropertyAlert>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Suburb).HasMaxLength(80);
            e.Property(x => x.Postcode).HasMaxLength(4);
            e.Property(x => x.PropertyType).HasMaxLength(20);
            e.Property(x => x.MinPrice).HasPrecision(14, 2);
            e.Property(x => x.MaxPrice).HasPrecision(14, 2);
            e.Property(x => x.ActiveSince).HasDefaultValueSql("clock_timestamp()");
            e.HasIndex(x => new { x.Active, x.Postcode });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_Alerts_Prices",
                "\"MinPrice\" >= 0 AND \"MaxPrice\" >= \"MinPrice\""));
        });
        b.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Subject).HasMaxLength(200);
            e.Property(x => x.Body).HasMaxLength(2000);
            e.HasIndex(x => new { x.AlertId, x.ListingId }).IsUnique();
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<PropertyAlert>().WithMany().HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Listing>().WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
