using System.Threading.Tasks;

namespace ToteBettingApp.Services;

public interface IAuthenticationService
{
    Task<string?> GetApiKeyAsync();
    Task SetApiKeyAsync(string apiKey, bool useTestEndpoint);
    bool UseTestEndpoint();
    bool IsAgeVerified();
    void SetAgeVerified(bool verified);
    bool TermsAccepted();
    void SetTermsAccepted(bool accepted);
}
