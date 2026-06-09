namespace GeoTrackerApp3.Services;

/// <summary>
/// Centralized environment configuration. Persists the selected environment
/// in Preferences so it survives app restarts.
/// </summary>
public static class EnvironmentConfig
{
    private const string PreferenceKey = "selected_environment";

    public enum AppEnvironment
    {
        Live,
        PreProd,
        QA,
        Config
    }

    /// <summary>
    /// Gets or sets the active environment. Changes are persisted immediately.
    /// When changed, the HttpClient used by ApiService is invalidated so
    /// subsequent calls use the new base URL.
    /// </summary>
    public static AppEnvironment Current
    {
        get
        {
            var stored = Preferences.Get(PreferenceKey, nameof(AppEnvironment.Live));
            return Enum.TryParse<AppEnvironment>(stored, out var env) ? env : AppEnvironment.Live;
        }
        set
        {
            Preferences.Set(PreferenceKey, value.ToString());
            OnEnvironmentChanged?.Invoke();
        }
    }

    /// <summary>
    /// Raised when the environment changes so services can invalidate cached state.
    /// </summary>
    public static event Action? OnEnvironmentChanged;

    // ??? URL helpers ????????????????????????????????????????????????

    public static string ApiBaseUrl => Current switch
    {
        AppEnvironment.PreProd => "https://picsapipreprod.ics.co.za",
        AppEnvironment.QA => "https://picsapiqa.ics.co.za",
        AppEnvironment.Config => "https://picsapiconfig.ics.co.za",
        _ => "https://picsapilive.ics.co.za"
    };

    public static string WebBaseUrl => Current switch
    {
        AppEnvironment.PreProd => "https://picsPreprod.ics.co.za",
        AppEnvironment.QA => "https://picsqabeta.ics.co.za",
        AppEnvironment.Config => "https://picsconfig.ics.co.za",
        _ => "https://pics.ics.co.za"
    };

    public static string ForgotPasswordUrl => $"{WebBaseUrl}/Home/ForgetPassword";

    public static string UserRegistrationUrl(int companyId) =>
        $"{WebBaseUrl}/Home/UserRegistration?companyID={companyId}&source=mobile&faDebug=1";

    /// <summary>
    /// Returns the API host name for the current environment (used for SSL validation).
    /// </summary>
    public static string ApiHost => new Uri(ApiBaseUrl).Host;

    /// <summary>
    /// All trusted hosts across all environments for WebView SSL bypass.
    /// </summary>
    public static HashSet<string> AllTrustedHosts { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "picsapilive.ics.co.za",
        "picsapipreprod.ics.co.za",
        "picsapiqa.ics.co.za",
        "picsapiconfig.ics.co.za",
        "pics.ics.co.za",
        "picsPreprod.ics.co.za",
        "picsqabeta.ics.co.za",
        "picsconfig.ics.co.za"
    };
}
