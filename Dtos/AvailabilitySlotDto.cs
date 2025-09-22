namespace PetHero.Api.Dtos;

public class AvailabilitySlotDto
{
    public string? Id { get; set; }
    public string? GuardianId { get; set; }
    public string? Start { get; set; }
    public string? End { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
