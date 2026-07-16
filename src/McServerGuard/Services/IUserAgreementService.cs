namespace McServerGuard.Services;

public interface IUserAgreementService
{
    bool IsAgreed { get; }
    DateTime? AgreedAt { get; }
    string? AgreedVersion { get; }
    void SetAgreed(string version);
    void Load();
    void Save();
}
