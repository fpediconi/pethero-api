namespace PetHero.Api.Dtos;

public class PaymentDto
{
    public string? Id { get; set; }
    public string? BookingId { get; set; }
    public decimal? Amount { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
    public string? CreatedAt { get; set; }
}
