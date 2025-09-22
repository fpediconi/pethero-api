using System.Collections.Generic;

namespace PetHero.Api.Dtos;

public class GuardianDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Bio { get; set; }
    public decimal? PricePerNight { get; set; }
    public List<string>? AcceptedTypes { get; set; }
    public List<string>? AcceptedSizes { get; set; }
    public List<string>? Photos { get; set; }
    public string? AvatarUrl { get; set; }
    public double? RatingAvg { get; set; }
    public int? RatingCount { get; set; }
    public string? City { get; set; }
}
