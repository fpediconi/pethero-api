using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PetHero.Api.Entities;

namespace PetHero.Api.Data;

public class PetHeroDbContext : DbContext
{
    public PetHeroDbContext(DbContextOptions<PetHeroDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<AvailabilitySlot> Availability => Set<AvailabilitySlot>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<PaymentVoucher> PaymentVouchers => Set<PaymentVoucher>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };

        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v ?? new List<string>(), jsonOptions),
            v => string.IsNullOrWhiteSpace(v)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (l1, l2) => (l1 ?? new List<string>()).SequenceEqual(l2 ?? new List<string>()),
            l => (l ?? new List<string>()).Aggregate(0, (a, v) => HashCode.Combine(a, v == null ? 0 : v.GetHashCode(StringComparison.Ordinal))),
            l => (l ?? new List<string>()).ToList());

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.Role).IsRequired();
        });

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.Property(p => p.DisplayName).IsRequired();
        });

        modelBuilder.Entity<Guardian>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.Property(g => g.Id).ValueGeneratedNever();
            entity.Property(g => g.PricePerNight).HasConversion<double>();
            entity.Property(g => g.AcceptedTypes)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);
            entity.Property(g => g.AcceptedSizes)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);
            entity.Property(g => g.Photos)
                .HasConversion(
                    v => JsonSerializer.Serialize(v ?? new List<string>(), jsonOptions),
                    v => string.IsNullOrWhiteSpace(v)
                        ? new List<string>()
                        : JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>())
                .Metadata.SetValueComparer(stringListComparer);
        });

        modelBuilder.Entity<AvailabilitySlot>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).ValueGeneratedNever();
            entity.Property(a => a.Start).IsRequired();
            entity.Property(a => a.End).IsRequired();
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Id).ValueGeneratedNever();
            entity.Property(b => b.TotalPrice).HasConversion<double?>();
        });

        modelBuilder.Entity<Pet>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedNever();
            entity.Property(p => p.OwnerId).IsRequired();
            entity.Property(p => p.Type).IsRequired();
            entity.Property(p => p.Size).IsRequired();
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).ValueGeneratedNever();
            entity.Property(r => r.Rating).IsRequired();
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).ValueGeneratedNever();
            entity.Property(m => m.Body).IsRequired();
        });

        modelBuilder.Entity<PaymentVoucher>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Id).ValueGeneratedNever();
            entity.Property(v => v.Amount).HasConversion<double>();
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedNever();
            entity.Property(p => p.Amount).HasConversion<double>();
        });
    }
}
