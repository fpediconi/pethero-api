using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using PetHero.Api.Data;
using PetHero.Api.Dtos;
using PetHero.Api.Entities;
using PetHero.Api.Configuration;
using PetHero.Api.Services;

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

var jwtOptions = new JwtOptions();
builder.Configuration.GetSection(JwtOptions.SectionName).Bind(jwtOptions);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();


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

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Json(new { message = "PetHero API ready" }));

var auth = app.MapGroup("/auth").WithTags("Auth");

auth.MapPost("/register", async (RegisterRequestDto payload, PetHeroDbContext db, ITokenService tokenService, CancellationToken cancellationToken) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    if (string.IsNullOrWhiteSpace(payload.Email) ||
        string.IsNullOrWhiteSpace(payload.Password) ||
        string.IsNullOrWhiteSpace(payload.Role))
    {
        return Results.BadRequest(new { message = "email, password and role are required" });
    }

    var email = payload.Email.Trim().ToLowerInvariant();
    var role = payload.Role.Trim().ToLowerInvariant();

    if (role != "owner" && role != "guardian")
    {
        return Results.BadRequest(new { message = "Role must be owner or guardian" });
    }

    var exists = await db.Users.AnyAsync(u => u.Email == email, cancellationToken);
    if (exists)
    {
        return Results.Conflict(new { message = "Email already registered" });
    }

    var user = new User
    {
        Email = email,
        Password = payload.Password,
        Role = role,
        IsLoggedIn = false,
        CreatedAt = DateTime.UtcNow.ToString("o")
    };

    await db.Users.AddAsync(user, cancellationToken);
    await db.SaveChangesAsync(cancellationToken);

    if (payload.Profile is not null)
    {
        var displayName = string.IsNullOrWhiteSpace(payload.Profile.DisplayName)
            ? email
            : payload.Profile.DisplayName.Trim();

        var profile = new Profile
        {
            UserId = user.Id,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
            Phone = payload.Profile.Phone,
            Location = payload.Profile.Location,
            Bio = payload.Profile.Bio,
            AvatarUrl = payload.Profile.AvatarUrl
        };

        await db.Profiles.AddAsync(profile, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        user.ProfileId = profile.Id;
        await db.SaveChangesAsync(cancellationToken);
    }

    var authUser = await BuildAuthenticatedUserDtoAsync(user, db, cancellationToken);
    var token = tokenService.GenerateToken(user);

    var response = new AuthResponseDto
    {
        Token = token,
        User = authUser
    };

    return Results.Ok(response);
});

auth.MapPost("/login", async (LoginRequestDto payload, PetHeroDbContext db, ITokenService tokenService, CancellationToken cancellationToken) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    if (string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Password))
    {
        return Results.BadRequest(new { message = "email and password are required" });
    }

    var email = payload.Email.Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    bool passwordOk = false;
    var stateChanged = false;

    if (user is not null && !string.IsNullOrWhiteSpace(user.Password))
    {
        // Si ya esta hasheada (BCrypt empieza con "$2")
        if (user.Password.StartsWith("$2"))
        {
            passwordOk = BCrypt.Net.BCrypt.Verify(payload.Password, user.Password);
        }
        else
        {
            // Compatibilidad hacia atras (texto plano); si matchea, re-hasheamos para endurecer
            if (string.Equals(user.Password, payload.Password, StringComparison.Ordinal))
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(payload.Password);
                passwordOk = true;
                stateChanged = true;
            }
        }
    }

    if (user is null || !passwordOk)
    {
        return Results.Unauthorized();
    }

    if (user.IsLoggedIn)
    {
        return Results.Conflict(new { message = "User already has an active session" });
    }

    user.IsLoggedIn = true;
    stateChanged = true;

    if (stateChanged)
    {
        await db.SaveChangesAsync(cancellationToken);
    }

    var authUser = await BuildAuthenticatedUserDtoAsync(user, db, cancellationToken);
    var token = tokenService.GenerateToken(user);

    var response = new AuthResponseDto
    {
        Token = token,
        User = authUser
    };

    return Results.Ok(response);
});
auth.MapPost("/logout", async (ClaimsPrincipal principal, PetHeroDbContext db, CancellationToken cancellationToken) =>
{
    var userId = ResolveUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    if (user.IsLoggedIn)
    {
        user.IsLoggedIn = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    return Results.Ok(new { success = true });
}).RequireAuthorization();

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
        Password = BCrypt.Net.BCrypt.HashPassword(payload.Password),
        Role = role,
        IsLoggedIn = false,
        CreatedAt = DateTime.UtcNow.ToString("o")
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
    // 1:1 con mock actual: json-server devolv�a []
    return Results.Ok(Array.Empty<object>());
}).WithTags("Availability");

var bookings = app.MapGroup("/bookings").WithTags("Bookings");

bookings.MapGet("", async (ClaimsPrincipal principal, string? guardianId, string? ownerId, string? status, string[]? states, PetHeroDbContext db, CancellationToken cancellationToken) =>
{
    var userId = ResolveUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var role = ResolveUserRole(principal);
    if (string.IsNullOrWhiteSpace(role))
    {
        return Results.Forbid();
    }

    var query = db.Bookings.AsNoTracking().AsQueryable();
    var userIdText = userId.Value.ToString();

    if (string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase))
    {
        var effectiveOwnerId = string.IsNullOrWhiteSpace(ownerId) ? userIdText : ownerId;
        if (!string.Equals(effectiveOwnerId, userIdText, StringComparison.Ordinal))
        {
            return Results.Forbid();
        }

        query = query.Where(b => b.OwnerId == userIdText);

        if (!string.IsNullOrWhiteSpace(guardianId))
        {
            query = query.Where(b => b.GuardianId == guardianId);
        }
    }
    else if (string.Equals(role, "guardian", StringComparison.OrdinalIgnoreCase))
    {
        var effectiveGuardianId = string.IsNullOrWhiteSpace(guardianId) ? userIdText : guardianId;
        if (!string.Equals(effectiveGuardianId, userIdText, StringComparison.Ordinal))
        {
            return Results.Forbid();
        }

        query = query.Where(b => b.GuardianId == userIdText);

        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            query = query.Where(b => b.OwnerId == ownerId);
        }
    }
    else
    {
        return Results.Forbid();
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

    var list = await query.OrderByDescending(b => b.CreatedAt).ToListAsync(cancellationToken);
    return Results.Ok(list);
}).RequireAuthorization();

bookings.MapGet("/{id}", async (string id, ClaimsPrincipal principal, PetHeroDbContext db, CancellationToken cancellationToken) =>
{
    var userId = ResolveUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var booking = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    if (booking is null)
    {
        return Results.NotFound();
    }

    if (!IsBookingAccessible(booking, userId.Value, ResolveUserRole(principal)))
    {
        return Results.Forbid();
    }

    return Results.Ok(booking);
}).RequireAuthorization();

bookings.MapPost("", async (BookingDto payload, ClaimsPrincipal principal, PetHeroDbContext db, CancellationToken cancellationToken) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    var userId = ResolveUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(payload.OwnerId) ||
        string.IsNullOrWhiteSpace(payload.GuardianId) ||
        string.IsNullOrWhiteSpace(payload.PetId) ||
        string.IsNullOrWhiteSpace(payload.Start) ||
        string.IsNullOrWhiteSpace(payload.End))
    {
        return Results.BadRequest(new { message = "ownerId, guardianId, petId, start and end are required" });
    }

    var role = ResolveUserRole(principal);
    var userIdText = userId.Value.ToString();

    if (string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase))
    {
        if (!string.Equals(payload.OwnerId, userIdText, StringComparison.Ordinal))
        {
            return Results.Forbid();
        }
    }
    else if (string.Equals(role, "guardian", StringComparison.OrdinalIgnoreCase))
    {
        if (!string.Equals(payload.GuardianId, userIdText, StringComparison.Ordinal))
        {
            return Results.Forbid();
        }
    }
    else
    {
        return Results.Forbid();
    }

    var booking = new Booking
    {
        Id = Guid.NewGuid().ToString(),
        OwnerId = payload.OwnerId!,
        GuardianId = payload.GuardianId!,
        PetId = payload.PetId!,
        Start = payload.Start!,
        End = payload.End!,
        Status = string.IsNullOrWhiteSpace(payload.Status) ? "REQUESTED" : payload.Status!,
        DepositPaid = payload.DepositPaid ?? false,
        TotalPrice = payload.TotalPrice,
        CreatedAt = DateTime.UtcNow.ToString("o")
    };

    db.Bookings.Add(booking);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/bookings/{booking.Id}", booking);
}).RequireAuthorization();

bookings.MapPut("/{id}", async (string id, BookingDto payload, ClaimsPrincipal principal, PetHeroDbContext db, CancellationToken cancellationToken) =>
{
    var userId = ResolveUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    if (booking is null)
    {
        return Results.NotFound();
    }

    if (!IsBookingAccessible(booking, userId.Value, ResolveUserRole(principal)))
    {
        return Results.Forbid();
    }

    if (!string.IsNullOrWhiteSpace(payload.OwnerId) && !string.Equals(payload.OwnerId, booking.OwnerId, StringComparison.Ordinal))
    {
        return Results.BadRequest(new { message = "ownerId cannot be changed" });
    }

    if (!string.IsNullOrWhiteSpace(payload.GuardianId) && !string.Equals(payload.GuardianId, booking.GuardianId, StringComparison.Ordinal))
    {
        return Results.BadRequest(new { message = "guardianId cannot be changed" });
    }

    if (!string.IsNullOrWhiteSpace(payload.PetId)) booking.PetId = payload.PetId!;
    if (!string.IsNullOrWhiteSpace(payload.Start)) booking.Start = payload.Start!;
    if (!string.IsNullOrWhiteSpace(payload.End)) booking.End = payload.End!;
    if (!string.IsNullOrWhiteSpace(payload.Status)) booking.Status = payload.Status!;
    if (payload.DepositPaid.HasValue) booking.DepositPaid = payload.DepositPaid.Value;
    if (payload.TotalPrice.HasValue) booking.TotalPrice = payload.TotalPrice;

    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(booking);
}).RequireAuthorization();

var pets = app.MapGroup("/pets").WithTags("Pets");

pets.MapGet("", async (
    ClaimsPrincipal principal,
    HttpRequest req,                    
    string? ownerId,                     
    PetHeroDbContext db,
    CancellationToken cancellationToken) =>
{
    var userId = ResolveUserId(principal);
    if (userId is null) return Results.Unauthorized();

    var role = ResolveUserRole(principal);
    var userIdText = userId.Value.ToString();

    if (string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase))
    {
        var result = await db.Pets.AsNoTracking()
            .Where(p => p.OwnerId == userIdText)
            .ToListAsync(cancellationToken);

        return Results.Ok(result);
    }

    if (string.Equals(role, "guardian", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Ok(new List<Pet>());
    }

    return Results.Forbid();
}).RequireAuthorization();


pets.MapGet("/{id}", async (string id, ClaimsPrincipal principal, PetHeroDbContext db, CancellationToken cancellationToken) =>
{
    var userId = ResolveUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var role = ResolveUserRole(principal);
    var userIdText = userId.Value.ToString();

    var pet = await db.Pets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    if (pet is null)
    {
        return Results.NotFound();
    }

    if (string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase))
    {
        if (!string.Equals(pet.OwnerId, userIdText, StringComparison.Ordinal))
        {
            return Results.Forbid();
        }
    }
    else if (string.Equals(role, "guardian", StringComparison.OrdinalIgnoreCase))
    {
        var hasBooking = await db.Bookings.AsNoTracking()
            .AnyAsync(b => b.PetId == pet.Id && b.GuardianId == userIdText, cancellationToken);
        if (!hasBooking)
        {
            return Results.Forbid();
        }
    }
    else
    {
        return Results.Forbid();
    }

    return Results.Ok(pet);
}).RequireAuthorization();

pets.MapPost("", async (PetDto payload, ClaimsPrincipal principal, PetHeroDbContext db, CancellationToken cancellationToken) =>
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

    var userId = ResolveUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var role = ResolveUserRole(principal);
    var userIdText = userId.Value.ToString();

    if (!string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(payload.OwnerId, userIdText, StringComparison.Ordinal))
    {
        return Results.Forbid();
    }

    var pet = new Pet
    {
        Id = Guid.NewGuid().ToString(),
        OwnerId = payload.OwnerId!,
        Name = payload.Name!,
        Type = payload.Type!,
        Breed = payload.Breed,
        Size = payload.Size!,
        PhotoUrl = payload.PhotoUrl,
        VaccineCalendarUrl = payload.VaccineCalendarUrl,
        Notes = payload.Notes
    };

    db.Pets.Add(pet);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/pets/{pet.Id}", pet);
}).RequireAuthorization();

pets.MapDelete("/{id}", async (string id, ClaimsPrincipal principal, PetHeroDbContext db, CancellationToken cancellationToken) =>
{
    var userId = ResolveUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var role = ResolveUserRole(principal);
    if (!string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Forbid();
    }

    var userIdText = userId.Value.ToString();
    var pet = await db.Pets.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    if (pet is null)
    {
        return Results.NotFound();
    }

    if (!string.Equals(pet.OwnerId, userIdText, StringComparison.Ordinal))
    {
        return Results.Forbid();
    }

    db.Pets.Remove(pet);
    await db.SaveChangesAsync(cancellationToken);

    return Results.NoContent();
}).RequireAuthorization();

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

vouchers.MapGet("", async (ClaimsPrincipal principal, string? bookingId, PetHeroDbContext db, CancellationToken cancellationToken) =>
{
    var userId = ResolveUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var role = ResolveUserRole(principal);
    if (string.IsNullOrWhiteSpace(role))
    {
        return Results.Forbid();
    }

    var query = db.PaymentVouchers.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(bookingId))
    {
        query = query.Where(v => v.BookingId == bookingId);
    }

    var vouchers = await query.ToListAsync(cancellationToken);
    if (vouchers.Count == 0)
    {
        return Results.Ok(vouchers);
    }

    var bookingIds = vouchers.Select(v => v.BookingId).Distinct().ToList();
    var bookings = await db.Bookings.AsNoTracking()
        .Where(b => bookingIds.Contains(b.Id))
        .ToDictionaryAsync(b => b.Id, cancellationToken);

    var filtered = vouchers.Where(v =>
        bookings.TryGetValue(v.BookingId, out var booking) &&
        IsBookingAccessible(booking, userId.Value, role)).ToList();

    return Results.Ok(filtered);
}).RequireAuthorization();

vouchers.MapGet("/{id}", async (string id, ClaimsPrincipal principal, PetHeroDbContext db, CancellationToken cancellationToken) =>
{
    var userId = ResolveUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var voucher = await db.PaymentVouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    if (voucher is null)
    {
        return Results.NotFound();
    }

    var booking = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == voucher.BookingId, cancellationToken);
    if (booking is null || !IsBookingAccessible(booking, userId.Value, ResolveUserRole(principal)))
    {
        return Results.Forbid();
    }

    return Results.Ok(voucher);
}).RequireAuthorization();

vouchers.MapPost("", async (PaymentVoucherDto payload, ClaimsPrincipal principal, PetHeroDbContext db, CancellationToken cancellationToken) =>
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

    var userId = ResolveUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == payload.BookingId, cancellationToken);
    if (booking is null)
    {
        return Results.BadRequest(new { message = "Booking not found" });
    }

    if (!IsBookingAccessible(booking, userId.Value, ResolveUserRole(principal)))
    {
        return Results.Forbid();
    }

    var voucher = new PaymentVoucher
    {
        Id = Guid.NewGuid().ToString(),
        BookingId = payload.BookingId!,
        Amount = payload.Amount.Value,
        DueDate = payload.DueDate!,
        Status = payload.Status!,
        CreatedAt = DateTime.UtcNow.ToString("o")
    };

    db.PaymentVouchers.Add(voucher);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/paymentVouchers/{voucher.Id}", voucher);
}).RequireAuthorization();

vouchers.MapPut("/{id}", async (string id, PaymentVoucherDto payload, ClaimsPrincipal principal, PetHeroDbContext db, CancellationToken cancellationToken) =>
{
    if (payload is null)
    {
        return Results.BadRequest(new { message = "Invalid body" });
    }

    var userId = ResolveUserId(principal);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    var voucher = await db.PaymentVouchers.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    if (voucher is null)
    {
        return Results.NotFound();
    }

    var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == voucher.BookingId, cancellationToken);
    if (booking is null || !IsBookingAccessible(booking, userId.Value, ResolveUserRole(principal)))
    {
        return Results.Forbid();
    }

    if (!string.IsNullOrWhiteSpace(payload.BookingId) && !string.Equals(payload.BookingId, voucher.BookingId, StringComparison.Ordinal))
    {
        return Results.BadRequest(new { message = "bookingId cannot be changed" });
    }

    if (payload.Amount.HasValue)
    {
        voucher.Amount = payload.Amount.Value;
    }

    if (!string.IsNullOrWhiteSpace(payload.DueDate))
    {
        voucher.DueDate = payload.DueDate!;
    }

    if (!string.IsNullOrWhiteSpace(payload.Status))
    {
        voucher.Status = payload.Status!;
    }

    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(voucher);
}).RequireAuthorization();

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


static int? ResolveUserId(ClaimsPrincipal principal)
{
    var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
    return int.TryParse(idValue, out var id) ? id : null;
}

static string? ResolveUserRole(ClaimsPrincipal principal)
{
    return principal.FindFirstValue(ClaimTypes.Role);
}

static bool IsBookingAccessible(Booking booking, int userId, string? role)
{
    var userIdText = userId.ToString();

    if (string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase))
    {
        return string.Equals(booking.OwnerId, userIdText, StringComparison.Ordinal);
    }

    if (string.Equals(role, "guardian", StringComparison.OrdinalIgnoreCase))
    {
        return string.Equals(booking.GuardianId, userIdText, StringComparison.Ordinal);
    }

    return false;
}

static async Task<AuthenticatedUserDto> BuildAuthenticatedUserDtoAsync(User user, PetHeroDbContext db, CancellationToken cancellationToken = default)
{
    ProfileSummaryDto? profileDto = null;

    if (user.ProfileId.HasValue)
    {
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == user.ProfileId.Value, cancellationToken);
        if (profile is null)
        {
            profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
        }

        if (profile is not null)
        {
            profileDto = new ProfileSummaryDto
            {
                Id = profile.Id,
                DisplayName = profile.DisplayName,
                Phone = profile.Phone,
                Location = profile.Location,
                Bio = profile.Bio,
                AvatarUrl = profile.AvatarUrl
            };
        }
    }
    else
    {
        var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
        if (profile is not null)
        {
            profileDto = new ProfileSummaryDto
            {
                Id = profile.Id,
                DisplayName = profile.DisplayName,
                Phone = profile.Phone,
                Location = profile.Location,
                Bio = profile.Bio,
                AvatarUrl = profile.AvatarUrl
            };
        }
    }

    return new AuthenticatedUserDto
    {
        Id = user.Id,
        Email = user.Email,
        Role = user.Role,
        ProfileId = profileDto?.Id ?? user.ProfileId,
        Profile = profileDto
    };
}

await app.RunAsync();





