using System;

namespace PetHero.Api.Entities;

public class Review
{
    public required string Id { get; set; }
    public required string BookingId { get; set; }
    public required string OwnerId { get; set; }
    public required string GuardianId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
}
