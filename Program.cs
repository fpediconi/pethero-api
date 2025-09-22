using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PetHero.Api.Data;
using PetHero.Api.Dtos;
using PetHero.Api.Entities;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:3000");

builder.Services.AddCors(options =>
{
    options.AddPolicy("spa", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    if (builder.Environment.IsDevelopment())
    {
        opts.SerializerOptions.WriteIndented = true;
    }
});

builder.Services.AddDbContext<PetHeroDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=pethero.db");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PetHeroDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.EnsureSeedDataAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("spa");

app.MapGet("/", () => Results.Json(new { message = "PetHero API ready" }));

var users = app.MapGroup("/users").WithTags("Users");

users.MapGet("", async (string? email, string? password, PetHeroDbContext db) =>
{
    // 1:1 con mock json-server: /users?email=&password=
    var query = db.Users.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(email))
    {
        query = query.Where(u => u.Email == email);
    }

    if (!string.IsNullOrWhiteSpace(password))
    {
        query = query.Where(u => u.Password == password);
    }

    var list = await query.ToListAsync();
    return Results.Ok(list);
});

users.MapGet("/{id:int}", async (int id, PetHeroDbContext db) =>
{
    var entity = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    return entity is null ? Results.NotFound() : Results.Ok(entity);
});

users.MapPost("", async (User payload, PetHeroDbContext db) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    if (string.IsNullOrWhiteSpace(payload.Email))
    {
        return Results.BadRequest(new { message = "Email is required" });
    }

    if (string.IsNullOrWhiteSpace(payload.Role))
    {
        return Results.BadRequest(new { message = "Role is required" });
    }

    var email = payload.Email.Trim();
    var role = payload.Role.Trim().ToLowerInvariant();

    if (role != "owner" && role != "guardian")
    {
        return Results.BadRequest(new { message = "Role must be owner or guardian" });
    }

    var exists = await db.Users.AnyAsync(u => u.Email == email);
    if (exists)
    {
        return Results.Conflict(new { message = "Email already registered" });
    }

    var entity = new User
    {
        Email = email,
        Password = payload.Password,
        Role = role,
        ProfileId = payload.ProfileId,
        CreatedAt = string.IsNullOrWhiteSpace(payload.CreatedAt) ? DateTime.UtcNow.ToString("o") : payload.CreatedAt!
    };

    db.Users.Add(entity);
    await db.SaveChangesAsync();

    return Results.Created($"/users/{entity.Id}", entity);
});

var profiles = app.MapGroup("/profiles").WithTags("Profiles");

profiles.MapGet("", async (int? userId, PetHeroDbContext db) =>
{
    // 1:1 con mock json-server: /profiles?userId=
    var query = db.Profiles.AsNoTracking().AsQueryable();
    if (userId.HasValue)
    {
        query = query.Where(p => p.UserId == userId.Value);
    }

    var list = await query.ToListAsync();
    return Results.Ok(list);
});

profiles.MapGet("/{id:int}", async (int id, PetHeroDbContext db) =>
{
    var entity = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    return entity is null ? Results.NotFound() : Results.Ok(entity);
});

profiles.MapPost("", async (Profile payload, PetHeroDbContext db) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    var userExists = await db.Users.AnyAsync(u => u.Id == payload.UserId);
    if (!userExists)
    {
        return Results.NotFound(new { message = "User not found" });
    }

    var entity = new Profile
    {
        UserId = payload.UserId,
        DisplayName = payload.DisplayName,
        Phone = payload.Phone,
        Location = payload.Location,
        Bio = payload.Bio,
        AvatarUrl = payload.AvatarUrl
    };

    db.Profiles.Add(entity);
    await db.SaveChangesAsync();

    return Results.Created($"/profiles/{entity.Id}", entity);
});

profiles.MapPut("/{id:int}", async (int id, Profile payload, PetHeroDbContext db) =>
{
    var entity = await db.Profiles.FirstOrDefaultAsync(p => p.Id == id);
    if (entity is null)
    {
        return Results.NotFound();
    }

    entity.DisplayName = payload.DisplayName;
    entity.Phone = payload.Phone;
    entity.Location = payload.Location;
    entity.Bio = payload.Bio;
    entity.AvatarUrl = payload.AvatarUrl;

    await db.SaveChangesAsync();

    return Results.Ok(entity);
});

var guardians = app.MapGroup("/guardians").WithTags("Guardians");

guardians.MapGet("", async (string? id, PetHeroDbContext db) =>
{
    // 1:1 con mock json-server: /guardians y /guardians?id=
    var query = db.Guardians.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(id))
    {
        query = query.Where(g => g.Id == id);
    }

    var list = await query.ToListAsync();
    return Results.Ok(list);
});

guardians.MapGet("/{id}", async (string id, PetHeroDbContext db) =>
{
    var entity = await db.Guardians.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
    return entity is null ? Results.NotFound() : Results.Ok(entity);
});

guardians.MapPost("", async (GuardianDto payload, PetHeroDbContext db) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    var id = string.IsNullOrWhiteSpace(payload.Id) ? Guid.NewGuid().ToString() : payload.Id!;
    var exists = await db.Guardians.AnyAsync(g => g.Id == id);
    if (exists)
    {
        return Results.Conflict(new { message = "Guardian already exists" });
    }

    var entity = new Guardian
    {
        Id = id,
        Name = payload.Name,
        Bio = payload.Bio,
        PricePerNight = payload.PricePerNight ?? 0,
        AcceptedTypes = (payload.AcceptedTypes ?? new List<string>()).ToList(),
        AcceptedSizes = (payload.AcceptedSizes ?? new List<string>()).ToList(),
        Photos = payload.Photos?.ToList(),
        AvatarUrl = payload.AvatarUrl,
        RatingAvg = payload.RatingAvg,
        RatingCount = payload.RatingCount,
        City = payload.City
    };

    db.Guardians.Add(entity);
    await db.SaveChangesAsync();

    return Results.Created($"/guardians/{entity.Id}", entity);
});

guardians.MapPut("/{id}", async (string id, GuardianDto payload, PetHeroDbContext db) =>
{
    var entity = await db.Guardians.FirstOrDefaultAsync(g => g.Id == id);
    if (entity is null)
    {
        return Results.NotFound();
    }

    if (payload.Name is not null) entity.Name = payload.Name;
    if (payload.Bio is not null) entity.Bio = payload.Bio;
    if (payload.PricePerNight.HasValue) entity.PricePerNight = payload.PricePerNight.Value;
    if (payload.AcceptedTypes is not null) entity.AcceptedTypes = payload.AcceptedTypes.ToList();
    if (payload.AcceptedSizes is not null) entity.AcceptedSizes = payload.AcceptedSizes.ToList();
    if (payload.Photos is not null) entity.Photos = payload.Photos.ToList();
    if (payload.AvatarUrl is not null) entity.AvatarUrl = payload.AvatarUrl;
    if (payload.RatingAvg.HasValue) entity.RatingAvg = payload.RatingAvg;
    if (payload.RatingCount.HasValue) entity.RatingCount = payload.RatingCount;
    if (payload.City is not null) entity.City = payload.City;

    await db.SaveChangesAsync();

    return Results.Ok(entity);
});

var availability = app.MapGroup("/availability").WithTags("Availability");

availability.MapGet("", async (string? guardianId, PetHeroDbContext db) =>
{
    // 1:1 con mock json-server: /availability?guardianId=
    if (string.IsNullOrWhiteSpace(guardianId))
    {
        return Results.BadRequest(new { message = "guardianId query parameter is required" });
    }

    var list = await db.Availability.AsNoTracking()
        .Where(a => a.GuardianId == guardianId)
        .OrderBy(a => a.Start)
        .ToListAsync();

    return Results.Ok(list);
});

availability.MapPost("", async (AvailabilitySlotDto payload, PetHeroDbContext db) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    if (string.IsNullOrWhiteSpace(payload.GuardianId))
    {
        return Results.BadRequest(new { message = "guardianId is required" });
    }

    if (string.IsNullOrWhiteSpace(payload.Start) || string.IsNullOrWhiteSpace(payload.End))
    {
        return Results.BadRequest(new { message = "start and end are required" });
    }

    var id = string.IsNullOrWhiteSpace(payload.Id) ? Guid.NewGuid().ToString() : payload.Id!;
    var exists = await db.Availability.AnyAsync(a => a.Id == id);
    if (exists)
    {
        return Results.Conflict(new { message = "Slot already exists" });
    }

    var entity = new AvailabilitySlot
    {
        Id = id,
        GuardianId = payload.GuardianId!,
        Start = payload.Start!,
        End = payload.End!,
        CreatedAt = string.IsNullOrWhiteSpace(payload.CreatedAt) ? DateTime.UtcNow.ToString("o") : payload.CreatedAt!,
        UpdatedAt = payload.UpdatedAt
    };

    db.Availability.Add(entity);
    await db.SaveChangesAsync();

    return Results.Created($"/availability/{entity.Id}", entity);
});

availability.MapPut("/{id}", async (string id, AvailabilitySlotDto payload, PetHeroDbContext db) =>
{
    var entity = await db.Availability.FirstOrDefaultAsync(a => a.Id == id);
    if (entity is null)
    {
        return Results.NotFound();
    }

    if (!string.IsNullOrWhiteSpace(payload.GuardianId)) entity.GuardianId = payload.GuardianId!;
    if (!string.IsNullOrWhiteSpace(payload.Start)) entity.Start = payload.Start!;
    if (!string.IsNullOrWhiteSpace(payload.End)) entity.End = payload.End!;
    if (!string.IsNullOrWhiteSpace(payload.CreatedAt)) entity.CreatedAt = payload.CreatedAt!;
    entity.UpdatedAt = string.IsNullOrWhiteSpace(payload.UpdatedAt) ? DateTime.UtcNow.ToString("o") : payload.UpdatedAt;

    await db.SaveChangesAsync();

    return Results.Ok(entity);
});

availability.MapDelete("/{id}", async (string id, PetHeroDbContext db) =>
{
    var entity = await db.Availability.FirstOrDefaultAsync(a => a.Id == id);
    if (entity is null)
    {
        return Results.NotFound();
    }

    db.Availability.Remove(entity);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapGet("/availability_exceptions", (string? guardianId) =>
{
    // 1:1 con mock actual: json-server devolvía []
    return Results.Ok(Array.Empty<object>());
}).WithTags("Availability");

var bookings = app.MapGroup("/bookings").WithTags("Bookings");

bookings.MapGet("", async (string? guardianId, string? ownerId, string? status, string[]? states, PetHeroDbContext db) =>
{
    // 1:1 con mock json-server: /bookings con filtros opcionales
    var query = db.Bookings.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(guardianId))
    {
        query = query.Where(b => b.GuardianId == guardianId);
    }

    if (!string.IsNullOrWhiteSpace(ownerId))
    {
        query = query.Where(b => b.OwnerId == ownerId);
    }

    var statusFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (!string.IsNullOrWhiteSpace(status))
    {
        statusFilters.Add(status);
    }

    if (states is not null)
    {
        foreach (var s in states)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                statusFilters.Add(s);
            }
        }
    }

    if (statusFilters.Count > 0)
    {
        query = query.Where(b => statusFilters.Contains(b.Status));
    }

    var list = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
    return Results.Ok(list);
});

bookings.MapGet("/{id}", async (string id, PetHeroDbContext db) =>
{
    var entity = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
    return entity is null ? Results.NotFound() : Results.Ok(entity);
});

bookings.MapPost("", async (BookingDto payload, PetHeroDbContext db) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    if (string.IsNullOrWhiteSpace(payload.OwnerId) ||
        string.IsNullOrWhiteSpace(payload.GuardianId) ||
        string.IsNullOrWhiteSpace(payload.PetId) ||
        string.IsNullOrWhiteSpace(payload.Start) ||
        string.IsNullOrWhiteSpace(payload.End))
    {
        return Results.BadRequest(new { message = "ownerId, guardianId, petId, start and end are required" });
    }

    var id = string.IsNullOrWhiteSpace(payload.Id) ? Guid.NewGuid().ToString() : payload.Id!;
    var exists = await db.Bookings.AnyAsync(b => b.Id == id);
    if (exists)
    {
        return Results.Conflict(new { message = "Booking already exists" });
    }

    var entity = new Booking
    {
        Id = id,
        OwnerId = payload.OwnerId!,
        GuardianId = payload.GuardianId!,
        PetId = payload.PetId!,
        Start = payload.Start!,
        End = payload.End!,
        Status = string.IsNullOrWhiteSpace(payload.Status) ? "REQUESTED" : payload.Status!,
        DepositPaid = payload.DepositPaid ?? false,
        TotalPrice = payload.TotalPrice,
        CreatedAt = string.IsNullOrWhiteSpace(payload.CreatedAt) ? DateTime.UtcNow.ToString("o") : payload.CreatedAt!
    };

    db.Bookings.Add(entity);
    await db.SaveChangesAsync();

    return Results.Created($"/bookings/{entity.Id}", entity);
});

bookings.MapPut("/{id}", async (string id, BookingDto payload, PetHeroDbContext db) =>
{
    var entity = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id);
    if (entity is null)
    {
        return Results.NotFound();
    }

    if (!string.IsNullOrWhiteSpace(payload.OwnerId)) entity.OwnerId = payload.OwnerId!;
    if (!string.IsNullOrWhiteSpace(payload.GuardianId)) entity.GuardianId = payload.GuardianId!;
    if (!string.IsNullOrWhiteSpace(payload.PetId)) entity.PetId = payload.PetId!;
    if (!string.IsNullOrWhiteSpace(payload.Start)) entity.Start = payload.Start!;
    if (!string.IsNullOrWhiteSpace(payload.End)) entity.End = payload.End!;
    if (!string.IsNullOrWhiteSpace(payload.Status)) entity.Status = payload.Status!;
    if (payload.DepositPaid.HasValue) entity.DepositPaid = payload.DepositPaid.Value;
    if (payload.TotalPrice.HasValue) entity.TotalPrice = payload.TotalPrice;
    if (!string.IsNullOrWhiteSpace(payload.CreatedAt)) entity.CreatedAt = payload.CreatedAt!;

    await db.SaveChangesAsync();

    return Results.Ok(entity);
});

var pets = app.MapGroup("/pets").WithTags("Pets");

pets.MapGet("", async (string? ownerId, PetHeroDbContext db) =>
{
    // 1:1 con mock json-server: /pets?ownerId=
    var query = db.Pets.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(ownerId))
    {
        query = query.Where(p => p.OwnerId == ownerId);
    }

    var list = await query.ToListAsync();
    return Results.Ok(list);
});

pets.MapGet("/{id}", async (string id, PetHeroDbContext db) =>
{
    var entity = await db.Pets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    return entity is null ? Results.NotFound() : Results.Ok(entity);
});

pets.MapPost("", async (PetDto payload, PetHeroDbContext db) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    if (string.IsNullOrWhiteSpace(payload.OwnerId) ||
        string.IsNullOrWhiteSpace(payload.Name) ||
        string.IsNullOrWhiteSpace(payload.Type) ||
        string.IsNullOrWhiteSpace(payload.Size))
    {
        return Results.BadRequest(new { message = "ownerId, name, type and size are required" });
    }

    var id = string.IsNullOrWhiteSpace(payload.Id) ? Guid.NewGuid().ToString() : payload.Id!;

    var entity = new Pet
    {
        Id = id,
        OwnerId = payload.OwnerId!,
        Name = payload.Name!,
        Type = payload.Type!,
        Breed = payload.Breed,
        Size = payload.Size!,
        PhotoUrl = payload.PhotoUrl,
        VaccineCalendarUrl = payload.VaccineCalendarUrl,
        Notes = payload.Notes
    };

    db.Pets.Add(entity);
    await db.SaveChangesAsync();

    return Results.Created($"/pets/{entity.Id}", entity);
});

pets.MapDelete("/{id}", async (string id, PetHeroDbContext db) =>
{
    var entity = await db.Pets.FirstOrDefaultAsync(p => p.Id == id);
    if (entity is null)
    {
        return Results.NotFound();
    }

    db.Pets.Remove(entity);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

var favorites = app.MapGroup("/favorites").WithTags("Favorites");

favorites.MapGet("", async (string? ownerId, string? guardianId, PetHeroDbContext db) =>
{
    // 1:1 con mock json-server: /favorites?ownerId=&guardianId=
    var query = db.Favorites.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(ownerId))
    {
        query = query.Where(f => f.OwnerId == ownerId);
    }

    if (!string.IsNullOrWhiteSpace(guardianId))
    {
        query = query.Where(f => f.GuardianId == guardianId);
    }

    var list = await query.ToListAsync();
    return Results.Ok(list);
});

favorites.MapPost("", async (FavoriteDto payload, PetHeroDbContext db) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    if (string.IsNullOrWhiteSpace(payload.OwnerId) || string.IsNullOrWhiteSpace(payload.GuardianId))
    {
        return Results.BadRequest(new { message = "ownerId and guardianId are required" });
    }

    var id = string.IsNullOrWhiteSpace(payload.Id) ? Guid.NewGuid().ToString() : payload.Id!;

    var entity = new Favorite
    {
        Id = id,
        OwnerId = payload.OwnerId!,
        GuardianId = payload.GuardianId!,
        CreatedAt = string.IsNullOrWhiteSpace(payload.CreatedAt) ? DateTime.UtcNow.ToString("o") : payload.CreatedAt!
    };

    db.Favorites.Add(entity);
    await db.SaveChangesAsync();

    return Results.Created($"/favorites/{entity.Id}", entity);
});

favorites.MapDelete("/{id}", async (string id, PetHeroDbContext db) =>
{
    var entity = await db.Favorites.FirstOrDefaultAsync(f => f.Id == id);
    if (entity is null)
    {
        return Results.NotFound();
    }

    db.Favorites.Remove(entity);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

var reviews = app.MapGroup("/reviews").WithTags("Reviews");

reviews.MapGet("", async (string? guardianId, PetHeroDbContext db) =>
{
    // 1:1 con mock json-server: /reviews?guardianId=
    if (string.IsNullOrWhiteSpace(guardianId))
    {
        return Results.BadRequest(new { message = "guardianId query parameter is required" });
    }

    var list = await db.Reviews.AsNoTracking()
        .Where(r => r.GuardianId == guardianId)
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync();

    return Results.Ok(list);
});

reviews.MapGet("/{id}", async (string id, PetHeroDbContext db) =>
{
    var entity = await db.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    return entity is null ? Results.NotFound() : Results.Ok(entity);
});

reviews.MapPost("", async (ReviewDto payload, PetHeroDbContext db) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    if (string.IsNullOrWhiteSpace(payload.BookingId) ||
        string.IsNullOrWhiteSpace(payload.OwnerId) ||
        string.IsNullOrWhiteSpace(payload.GuardianId) ||
        !payload.Rating.HasValue)
    {
        return Results.BadRequest(new { message = "bookingId, ownerId, guardianId and rating are required" });
    }

    var id = string.IsNullOrWhiteSpace(payload.Id) ? Guid.NewGuid().ToString() : payload.Id!;
    var exists = await db.Reviews.AnyAsync(r => r.Id == id);
    if (exists)
    {
        return Results.Conflict(new { message = "Review already exists" });
    }

    var entity = new Review
    {
        Id = id,
        BookingId = payload.BookingId!,
        OwnerId = payload.OwnerId!,
        GuardianId = payload.GuardianId!,
        Rating = payload.Rating.Value,
        Comment = payload.Comment,
        CreatedAt = string.IsNullOrWhiteSpace(payload.CreatedAt) ? DateTime.UtcNow.ToString("o") : payload.CreatedAt!
    };

    db.Reviews.Add(entity);
    await db.SaveChangesAsync();

    return Results.Created($"/reviews/{entity.Id}", entity);
});

reviews.MapPut("/{id}", async (string id, ReviewDto payload, PetHeroDbContext db) =>
{
    var entity = await db.Reviews.FirstOrDefaultAsync(r => r.Id == id);
    if (entity is null)
    {
        return Results.NotFound();
    }

    if (!string.IsNullOrWhiteSpace(payload.BookingId)) entity.BookingId = payload.BookingId!;
    if (!string.IsNullOrWhiteSpace(payload.OwnerId)) entity.OwnerId = payload.OwnerId!;
    if (!string.IsNullOrWhiteSpace(payload.GuardianId)) entity.GuardianId = payload.GuardianId!;
    if (payload.Rating.HasValue) entity.Rating = payload.Rating.Value;
    if (payload.Comment is not null) entity.Comment = payload.Comment;
    if (!string.IsNullOrWhiteSpace(payload.CreatedAt)) entity.CreatedAt = payload.CreatedAt!;

    await db.SaveChangesAsync();

    return Results.Ok(entity);
});

reviews.MapDelete("/{id}", async (string id, PetHeroDbContext db) =>
{
    var entity = await db.Reviews.FirstOrDefaultAsync(r => r.Id == id);
    if (entity is null)
    {
        return Results.NotFound();
    }

    db.Reviews.Remove(entity);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

var messages = app.MapGroup("/messages").WithTags("Messages");

messages.MapGet("", async (string? fromUserId, string? toUserId, PetHeroDbContext db) =>
{
    // 1:1 con mock json-server: /messages?fromUserId=&toUserId=
    var query = db.Messages.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(fromUserId))
    {
        query = query.Where(m => m.FromUserId == fromUserId);
    }

    if (!string.IsNullOrWhiteSpace(toUserId))
    {
        query = query.Where(m => m.ToUserId == toUserId);
    }

    var list = await query.OrderBy(m => m.CreatedAt).ToListAsync();
    return Results.Ok(list);
});

messages.MapGet("/{id}", async (string id, PetHeroDbContext db) =>
{
    var entity = await db.Messages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
    return entity is null ? Results.NotFound() : Results.Ok(entity);
});

messages.MapPost("", async (MessageDto payload, PetHeroDbContext db) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    if (string.IsNullOrWhiteSpace(payload.FromUserId) ||
        string.IsNullOrWhiteSpace(payload.ToUserId) ||
        string.IsNullOrWhiteSpace(payload.Body))
    {
        return Results.BadRequest(new { message = "fromUserId, toUserId and body are required" });
    }

    var id = string.IsNullOrWhiteSpace(payload.Id) ? Guid.NewGuid().ToString() : payload.Id!;
    var entity = new Message
    {
        Id = id,
        FromUserId = payload.FromUserId!,
        ToUserId = payload.ToUserId!,
        Body = payload.Body!,
        CreatedAt = string.IsNullOrWhiteSpace(payload.CreatedAt) ? DateTime.UtcNow.ToString("o") : payload.CreatedAt!,
        BookingId = payload.BookingId,
        Status = string.IsNullOrWhiteSpace(payload.Status) ? "SENT" : payload.Status
    };

    db.Messages.Add(entity);
    await db.SaveChangesAsync();

    return Results.Created($"/messages/{entity.Id}", entity);
});

var vouchers = app.MapGroup("/paymentVouchers").WithTags("PaymentVouchers");

vouchers.MapGet("", async (string? bookingId, PetHeroDbContext db) =>
{
    // 1:1 con mock json-server: /paymentVouchers?bookingId=
    var query = db.PaymentVouchers.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(bookingId))
    {
        query = query.Where(v => v.BookingId == bookingId);
    }

    var list = await query.ToListAsync();
    return Results.Ok(list);
});

vouchers.MapGet("/{id}", async (string id, PetHeroDbContext db) =>
{
    var entity = await db.PaymentVouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
    return entity is null ? Results.NotFound() : Results.Ok(entity);
});

vouchers.MapPost("", async (PaymentVoucherDto payload, PetHeroDbContext db) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    if (string.IsNullOrWhiteSpace(payload.BookingId) ||
        !payload.Amount.HasValue ||
        string.IsNullOrWhiteSpace(payload.DueDate) ||
        string.IsNullOrWhiteSpace(payload.Status))
    {
        return Results.BadRequest(new { message = "bookingId, amount, dueDate and status are required" });
    }

    var id = string.IsNullOrWhiteSpace(payload.Id) ? Guid.NewGuid().ToString() : payload.Id!;
    var exists = await db.PaymentVouchers.AnyAsync(v => v.Id == id);
    if (exists)
    {
        return Results.Conflict(new { message = "Voucher already exists" });
    }

    var entity = new PaymentVoucher
    {
        Id = id,
        BookingId = payload.BookingId!,
        Amount = payload.Amount.Value,
        DueDate = payload.DueDate!,
        Status = payload.Status!,
        CreatedAt = string.IsNullOrWhiteSpace(payload.CreatedAt) ? DateTime.UtcNow.ToString("o") : payload.CreatedAt
    };

    db.PaymentVouchers.Add(entity);
    await db.SaveChangesAsync();

    return Results.Created($"/paymentVouchers/{entity.Id}", entity);
});

vouchers.MapPut("/{id}", async (string id, PaymentVoucherDto payload, PetHeroDbContext db) =>
{
    var entity = await db.PaymentVouchers.FirstOrDefaultAsync(v => v.Id == id);
    if (entity is null)
    {
        return Results.NotFound();
    }

    if (!string.IsNullOrWhiteSpace(payload.BookingId)) entity.BookingId = payload.BookingId!;
    if (payload.Amount.HasValue) entity.Amount = payload.Amount.Value;
    if (!string.IsNullOrWhiteSpace(payload.DueDate)) entity.DueDate = payload.DueDate!;
    if (!string.IsNullOrWhiteSpace(payload.Status)) entity.Status = payload.Status!;
    if (!string.IsNullOrWhiteSpace(payload.CreatedAt)) entity.CreatedAt = payload.CreatedAt;

    await db.SaveChangesAsync();

    return Results.Ok(entity);
});

var payments = app.MapGroup("/payments").WithTags("Payments");

payments.MapPost("", async (PaymentDto payload, PetHeroDbContext db) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    if (string.IsNullOrWhiteSpace(payload.BookingId) ||
        !payload.Amount.HasValue ||
        string.IsNullOrWhiteSpace(payload.Type) ||
        string.IsNullOrWhiteSpace(payload.Status))
    {
        return Results.BadRequest(new { message = "bookingId, amount, type and status are required" });
    }

    var id = string.IsNullOrWhiteSpace(payload.Id) ? Guid.NewGuid().ToString() : payload.Id!;

    var entity = new Payment
    {
        Id = id,
        BookingId = payload.BookingId!,
        Amount = payload.Amount.Value,
        Type = payload.Type!,
        Status = payload.Status!,
        CreatedAt = string.IsNullOrWhiteSpace(payload.CreatedAt) ? DateTime.UtcNow.ToString("o") : payload.CreatedAt!
    };

    db.Payments.Add(entity);
    await db.SaveChangesAsync();

    return Results.Created($"/payments/{entity.Id}", entity);
});

await app.RunAsync();
