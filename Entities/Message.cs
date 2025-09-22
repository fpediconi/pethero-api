using System;

namespace PetHero.Api.Entities;

public class Message
{
    public required string Id { get; set; }
    public required string FromUserId { get; set; }
    public required string ToUserId { get; set; }
    public required string Body { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public string? BookingId { get; set; }
    public string? Status { get; set; }
}
