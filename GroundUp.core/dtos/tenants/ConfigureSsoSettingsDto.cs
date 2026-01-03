namespace GroundUp.core.dtos.tenants
{
    /// <summary>
    /// DTO for configuring SSO auto-join settings.
    /// </summary>
    public class ConfigureSsoSettingsDto
    {
        public List<string>? SsoAutoJoinDomains { get; set; }
        public int? SsoAutoJoinRoleId { get; set; }
    }
}
