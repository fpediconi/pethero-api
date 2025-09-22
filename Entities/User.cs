using System;

namespace PetHero.Api.Entities;

public class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public string? Password { get; set; }
    public required string Role { get; set; }
    public int? ProfileId { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
}
