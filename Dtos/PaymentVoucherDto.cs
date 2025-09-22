namespace PetHero.Api.Dtos;

public class PaymentVoucherDto
{
    public string? Id { get; set; }
    public string? BookingId { get; set; }
    public decimal? Amount { get; set; }
    public string? DueDate { get; set; }
    public string? Status { get; set; }
    public string? CreatedAt { get; set; }
}
