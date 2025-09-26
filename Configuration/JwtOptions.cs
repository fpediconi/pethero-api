namespace PetHero.Api.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = "pethero-dev-secret-change-me";

    public string Issuer { get; set; } = "PetHero.Api";

    public string Audience { get; set; } = "PetHero.Client";

    public int ExpiresMinutes { get; set; } = 120;
}
