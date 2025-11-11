using GeoTrackerApp3.Views;

namespace GeoTrackerApp3
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Use NavigationPage to allow page navigation
            MainPage = new NavigationPage(new LoginPage());
        }
    }
}