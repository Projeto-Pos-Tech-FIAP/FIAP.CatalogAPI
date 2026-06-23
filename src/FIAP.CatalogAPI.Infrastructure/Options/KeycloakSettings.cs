namespace FIAP.CatalogAPI.Infrastructure.Options;

public class KeycloakSettings
{
    public const string SectionName = "Keycloak";

    public string Authority { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public string BaseUrl { get; set; } = null!;
    public string Realm { get; set; } = null!;
}
