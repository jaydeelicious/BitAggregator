using BitAggregator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BitAggregator.Infrastructure.Persistence;

public sealed class BitAggregatorDbContext(DbContextOptions<BitAggregatorDbContext> options) : DbContext(options)
{
    public DbSet<AggregatedPrice> AggregatedPrices => Set<AggregatedPrice>();

    public DbSet<SourcePrice> SourcePrices => Set<SourcePrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AggregatedPrice>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Instrument).IsRequired().HasMaxLength(30);

            entity.Property(x => x.Timestamp).IsRequired();

            entity.Property(x => x.Price).IsRequired();

            entity.Property(x => x.CreatedAt).IsRequired();

            entity.HasIndex(x => new
            {
                x.Instrument,
                x.Timestamp
            }).IsUnique();

            entity.HasMany(x => x.SourcePrices)
                .WithOne(x => x.AggregatedPrice)
                .HasForeignKey(x => x.AggregatedPriceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourcePrice>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.SourceName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Price).IsRequired();

            entity.HasIndex(x => new
            {
                x.AggregatedPriceId,
                x.SourceName
            }).IsUnique();
        });
    }
}