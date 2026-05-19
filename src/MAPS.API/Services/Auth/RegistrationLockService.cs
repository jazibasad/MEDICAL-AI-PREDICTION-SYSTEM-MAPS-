namespace MAPS.API.Services.Auth;

public interface IRegistrationLockService
{
    bool IsLocked { get; }
    bool RequiresAdminApproval { get; }
}

public class RegistrationLockService : IRegistrationLockService
{
    private readonly IConfiguration _config;

    public RegistrationLockService(IConfiguration config)
    {
        _config = config;
    }

    public bool IsLocked =>
        _config.GetValue<bool>("RegistrationLock:IsLocked");

    public bool RequiresAdminApproval =>
        _config.GetValue<bool>("RegistrationLock:RequireAdminApproval");
}
