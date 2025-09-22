using System;

namespace PetHero.Api.Entities;

public class AvailabilitySlot
{
    public required string Id { get; set; }
    public required string GuardianId { get; set; }
    public required string Start { get; set; }
    public required string End { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public string? UpdatedAt { get; set; }
}
