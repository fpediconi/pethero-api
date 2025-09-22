using System.Collections.Generic;

namespace PetHero.Api.Entities;

public class Guardian
{
    public required string Id { get; set; }
    public string? Name { get; set; }
    public string? Bio { get; set; }
    public decimal PricePerNight { get; set; }
    public List<string> AcceptedTypes { get; set; } = new();
    public List<string> AcceptedSizes { get; set; } = new();
    public List<string>? Photos { get; set; }
    public string? AvatarUrl { get; set; }
    public double? RatingAvg { get; set; }
    public int? RatingCount { get; set; }
    public string? City { get; set; }
}
