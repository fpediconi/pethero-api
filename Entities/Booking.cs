using System;

namespace PetHero.Api.Entities;

public class Booking
{
    public required string Id { get; set; }
    public required string OwnerId { get; set; }
    public required string GuardianId { get; set; }
    public required string PetId { get; set; }
    public required string Start { get; set; }
    public required string End { get; set; }
    public required string Status { get; set; }
    public bool DepositPaid { get; set; }
    public decimal? TotalPrice { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
}
