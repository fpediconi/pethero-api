namespace PetHero.Api.Dtos;

public class PetDto
{
    public string? Id { get; set; }
    public string? OwnerId { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Breed { get; set; }
    public string? Size { get; set; }
    public string? PhotoUrl { get; set; }
    public string? VaccineCalendarUrl { get; set; }
    public string? Notes { get; set; }
}
