using System;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace ToteBettingApp.Helpers;

public static class SecureStorageHelper
{
    private const string ApiKeyKey = "a149b1adacad4436bd30d9adb91cc16d";
    private const string UseTestKey = "tote_use_test";
    private const string AgeVerifiedKey = "tote_age_verified";
    private const string TermsAcceptedKey = "tote_terms_accepted";

    public static async Task SetApiKeyAsync(string apiKey)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // On some platforms, we need to set a placeholder value to "clear" it
                // Since SecureStorage doesn't have a Remove method, we set it to empty
                // which will be treated as "not set" on the next read
                await SecureStorage.Default.SetAsync(ApiKeyKey, string.Empty);
            }
            else
            {
                await SecureStorage.Default.SetAsync(ApiKeyKey, apiKey);
            }
            // Note: UseTest endpoint is set separately via SetUseTest() method
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting API key: {ex.Message}");
            throw;
        }
    }

    public static async Task<string?> GetApiKeyAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(ApiKeyKey);
        }
        catch
        {
            return null;
        }
    }

    public static void SetUseTest(bool useTest) => Preferences.Default.Set(UseTestKey, useTest);

    public static bool GetUseTest() => Preferences.Default.Get(UseTestKey, true);

    public static void SetAgeVerified(bool verified) => Preferences.Default.Set(AgeVerifiedKey, verified);

    public static bool GetAgeVerified() => Preferences.Default.Get(AgeVerifiedKey, false);

    public static void SetTermsAccepted(bool accepted) => Preferences.Default.Set(TermsAcceptedKey, accepted);

    public static bool GetTermsAccepted() => Preferences.Default.Get(TermsAcceptedKey, false);
}
