using System;

namespace PetHero.Api.Entities;

public class PaymentVoucher
{
    public required string Id { get; set; }
    public required string BookingId { get; set; }
    public decimal Amount { get; set; }
    public required string DueDate { get; set; }
    public required string Status { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
}



