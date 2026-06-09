using GeoTrackerApp3.Services;

namespace GeoTrackerApp3.Views;

public partial class EnvironmentSettingsPage : ContentPage
{
    public EnvironmentSettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SetRadioForCurrentEnvironment();
        UpdateLabels();
    }

    private void SetRadioForCurrentEnvironment()
    {
        var current = EnvironmentConfig.Current;
        RadioLive.IsChecked = current == EnvironmentConfig.AppEnvironment.Live;
        RadioPreProd.IsChecked = current == EnvironmentConfig.AppEnvironment.PreProd;
        RadioQA.IsChecked = current == EnvironmentConfig.AppEnvironment.QA;
        RadioConfig.IsChecked = current == EnvironmentConfig.AppEnvironment.Config;
    }

    private void OnEnvironmentChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (!e.Value) return; // Only handle the newly checked radio

        if (RadioLive.IsChecked)
            EnvironmentConfig.Current = EnvironmentConfig.AppEnvironment.Live;
        else if (RadioPreProd.IsChecked)
            EnvironmentConfig.Current = EnvironmentConfig.AppEnvironment.PreProd;
        else if (RadioQA.IsChecked)
            EnvironmentConfig.Current = EnvironmentConfig.AppEnvironment.QA;
        else if (RadioConfig.IsChecked)
            EnvironmentConfig.Current = EnvironmentConfig.AppEnvironment.Config;

        UpdateLabels();
    }

    private void UpdateLabels()
    {
        CurrentEnvLabel.Text = EnvironmentConfig.Current.ToString();
        CurrentApiUrlLabel.Text = EnvironmentConfig.ApiBaseUrl;
        ApiUrlPreview.Text = $"API: {EnvironmentConfig.ApiBaseUrl}";
        WebUrlPreview.Text = $"Web: {EnvironmentConfig.WebBaseUrl}";
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
