namespace PetHero.Api.Dtos;

public class BookingDto
{
    public string? Id { get; set; }
    public string? OwnerId { get; set; }
    public string? GuardianId { get; set; }
    public string? PetId { get; set; }
    public string? Start { get; set; }
    public string? End { get; set; }
    public string? Status { get; set; }
    public bool? DepositPaid { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? CreatedAt { get; set; }
}
