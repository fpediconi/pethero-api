namespace PetHero.Api.Entities;

public class Profile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
}
