using System;

namespace PetHero.Api.Entities;

public class Favorite
{
    public required string Id { get; set; }
    public required string OwnerId { get; set; }
    public required string GuardianId { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
}
