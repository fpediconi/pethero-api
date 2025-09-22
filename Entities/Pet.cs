namespace PetHero.Api.Entities;

public class Pet
{
    public required string Id { get; set; }
    public required string OwnerId { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public string? Breed { get; set; }
    public required string Size { get; set; }
    public string? PhotoUrl { get; set; }
    public string? VaccineCalendarUrl { get; set; }
    public string? Notes { get; set; }
}
