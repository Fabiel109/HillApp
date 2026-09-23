namespace HillApp.Services;

public sealed class AdminAccessService
{
    private readonly string _ownerEmail;

    public AdminAccessService(IConfiguration configuration)
    {
        _ownerEmail = configuration["Admin:OwnerEmail"]?.Trim()
            ?? "cfabiel31@gmail.com";
    }

    public bool IsAdmin(CurrentUser? user) =>
        user is not null
        && string.Equals(user.Email.Trim(), _ownerEmail, StringComparison.OrdinalIgnoreCase);
}
