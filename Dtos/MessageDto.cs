namespace PetHero.Api.Dtos;

public class MessageDto
{
    public string? Id { get; set; }
    public string? FromUserId { get; set; }
    public string? ToUserId { get; set; }
    public string? Body { get; set; }
    public string? CreatedAt { get; set; }
    public string? BookingId { get; set; }
    public string? Status { get; set; }
}
