namespace PetHero.Api.Dtos;

public class LoginRequestDto
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}

public class RegisterRequestDto
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Role { get; set; }
    public RegisterProfileDto? Profile { get; set; }
}

public class RegisterProfileDto
{
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
}

public class AuthResponseDto
{
    public required string Token { get; set; }
    public required AuthenticatedUserDto User { get; set; }
}

public class AuthenticatedUserDto
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string Role { get; set; }
    public int? ProfileId { get; set; }
    public ProfileSummaryDto? Profile { get; set; }
}

public class ProfileSummaryDto
{
    public int Id { get; set; }
    public required string DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
}
