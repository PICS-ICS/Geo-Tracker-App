using GeoTrackerApp3.Views;

namespace GeoTrackerApp3
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Check if user has an existing session (token stored from previous login)
            MainPage = new NavigationPage(new LoginPage());
            _ = TryResumeSessionAsync();
        }

        private async Task TryResumeSessionAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("api_token");
                if (!string.IsNullOrEmpty(token))
                {
                    // User is still logged in — go directly to HomePage
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        MainPage = new NavigationPage(new HomePage());
                    });
                }
            }
            catch
            {
                // SecureStorage may fail on some devices; fall through to login
            }
        }
    }
}