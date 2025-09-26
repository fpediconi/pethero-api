using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PetHero.Api.Entities;

namespace PetHero.Api.Data;

public static class SeedData
{
    public static async Task EnsureSeedDataAsync(PetHeroDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var owner = new User
        {
            Id = 1,
            Email = "owner@pethero.test",
            Password = "owner123",
            Role = "owner",
            IsLoggedIn = false,
            CreatedAt = now.ToString("o")
        };

        var guardianUser = new User
        {
            Id = 2,
            Email = "guardian@pethero.test",
            Password = "guardian123",
            Role = "guardian",
            IsLoggedIn = false,
            CreatedAt = now.ToString("o")
        };

        var ownerProfile = new Profile
        {
            Id = 1,
            UserId = owner.Id,
            DisplayName = "Alicia Duena",
            Phone = "+54 9 11 0000-0001",
            Location = "Buenos Aires",
            Bio = "Duena responsable buscando guardianes de confianza.",
            AvatarUrl = "https://i.pravatar.cc/160?img=48"
        };

        var guardianProfile = new Profile
        {
            Id = 2,
            UserId = guardianUser.Id,
            DisplayName = "Bruno Guardian",
            Phone = "+54 9 11 0000-0002",
            Location = "Buenos Aires",
            Bio = "Cuidador con patio amplio y mucho carino por los animales.",
            AvatarUrl = "https://i.pravatar.cc/160?img=12"
        };

        owner.ProfileId = ownerProfile.Id;
        guardianUser.ProfileId = guardianProfile.Id;

        var guardian = new Guardian
        {
            Id = guardianUser.Id.ToString(),
            Name = guardianProfile.DisplayName,
            Bio = guardianProfile.Bio,
            AvatarUrl = guardianProfile.AvatarUrl,
            PricePerNight = 7500,
            AcceptedTypes = new List<string> { "DOG", "CAT" },
            AcceptedSizes = new List<string> { "SMALL", "MEDIUM" },
            Photos = new List<string>(),
            RatingAvg = 4.8,
            RatingCount = 12,
            City = guardianProfile.Location
        };

        var availability = new AvailabilitySlot
        {
            Id = "slot-1",
            GuardianId = guardian.Id,
            Start = now.AddDays(3).ToString("o"),
            End = now.AddDays(10).ToString("o"),
            CreatedAt = now.ToString("o")
        };

        var pet = new Pet
        {
            Id = "pet-1",
            OwnerId = owner.Id.ToString(),
            Name = "Luna",
            Type = "DOG",
            Breed = "Mestiza",
            Size = "MEDIUM",
            PhotoUrl = "https://images.dog.ceo/breeds/spaniel-brittany/n02101388_6057.jpg",
            Notes = "Super sociable y duerme toda la noche.",
            VaccineCalendarUrl = "https://example.com/calendario-luna.pdf"
        };

        var booking = new Booking
        {
            Id = "booking-1",
            OwnerId = owner.Id.ToString(),
            GuardianId = guardian.Id,
            PetId = pet.Id,
            Start = now.AddDays(5).ToString("o"),
            End = now.AddDays(8).ToString("o"),
            Status = "ACCEPTED",
            DepositPaid = true,
            TotalPrice = 22500,
            CreatedAt = now.ToString("o")
        };

        var favorite = new Favorite
        {
            Id = Guid.NewGuid().ToString(),
            OwnerId = owner.Id.ToString(),
            GuardianId = guardian.Id,
            CreatedAt = now.ToString("o")
        };

        var review = new Review
        {
            Id = "rev-1",
            BookingId = booking.Id,
            OwnerId = owner.Id.ToString(),
            GuardianId = guardian.Id,
            Rating = 5,
            Comment = "Excelente cuidado, Luna volvio feliz.",
            CreatedAt = now.ToString("o")
        };

        var message = new Message
        {
            Id = "msg-1",
            FromUserId = owner.Id.ToString(),
            ToUserId = guardianUser.Id.ToString(),
            Body = "Hola Bruno, tenes disponibilidad para la proxima semana?",
            CreatedAt = now.AddMinutes(-30).ToString("o"),
            BookingId = booking.Id,
            Status = "SENT"
        };

        var voucher = new PaymentVoucher
        {
            Id = "vouch-1",
            BookingId = booking.Id,
            Amount = 11250,
            DueDate = now.AddDays(2).ToString("o"),
            Status = "ISSUED",
            CreatedAt = now.ToString("o")
        };

        var payment = new Payment
        {
            Id = "pay-1",
            BookingId = booking.Id,
            Amount = 11250,
            Type = "DEPOSIT",
            Status = "APPROVED",
            CreatedAt = now.AddMinutes(-10).ToString("o")
        };

        await db.Users.AddRangeAsync(new[] { owner, guardianUser }, cancellationToken);
        await db.Profiles.AddRangeAsync(new[] { ownerProfile, guardianProfile }, cancellationToken);
        await db.Guardians.AddAsync(guardian, cancellationToken);
        await db.Availability.AddAsync(availability, cancellationToken);
        await db.Pets.AddAsync(pet, cancellationToken);
        await db.Bookings.AddAsync(booking, cancellationToken);
        await db.Favorites.AddAsync(favorite, cancellationToken);
        await db.Reviews.AddAsync(review, cancellationToken);
        await db.Messages.AddAsync(message, cancellationToken);
        await db.PaymentVouchers.AddAsync(voucher, cancellationToken);
        await db.Payments.AddAsync(payment, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}

