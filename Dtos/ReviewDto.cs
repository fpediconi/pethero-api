namespace PetHero.Api.Dtos;

public class ReviewDto
{
    public string? Id { get; set; }
    public string? BookingId { get; set; }
    public string? OwnerId { get; set; }
    public string? GuardianId { get; set; }
    public int? Rating { get; set; }
    public string? Comment { get; set; }
    public string? CreatedAt { get; set; }
}
