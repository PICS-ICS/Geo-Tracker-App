using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GeoTrackerApp3.Models;
using GeoTrackerApp3.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace GeoTrackerApp3.Views
{
    /// <summary>
    /// Add / Edit Person form. Pass a memberId to edit an existing person, or
    /// 0 (the default) to add a new one. Mirrors the web MemberTaskDetailMemberDetail view.
    /// </summary>
    public partial class EditPersonPage : ContentPage
    {
        private static readonly Regex EmailRegex =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly int _memberId;       // 0 => add new person
        private readonly bool _isAddMode;

        private string _token = string.Empty;
        private int _loggedInMemberId;

        // Selected leader (must come from the suggestion list, like the web form).
        private int? _selectedLeaderId;
        private string _selectedLeaderName = string.Empty;

        private string _sex = string.Empty;   // "M" or "F"
        private bool _dobSet;

        private CancellationTokenSource? _leaderSearchCts;
        private bool _suppressLeaderSearch;

        public ObservableCollection<LeaderItem> LeaderResults { get; } = new();

        /// <summary>Raised when a person is successfully saved, so callers can refresh.</summary>
        public event EventHandler? Saved;

        public EditPersonPage(int memberId = 0)
        {
            InitializeComponent();

            _memberId = memberId;
            _isAddMode = memberId <= 0;

            LeaderSuggestions.ItemsSource = LeaderResults;

            HeaderTitleLabel.Text = _isAddMode ? "Add Person" : "Edit Person";
            HeaderSubtitleLabel.Text = _isAddMode ? "Create a new person" : "Update person details";
            SaveButton.Text = _isAddMode ? "Add Person" : "Save Changes";

            // Role selection is only required when adding a new user (web AddUserFlag).
            RoleSection.IsVisible = _isAddMode;

            // Sensible DOB default (not today, which is never a birth date).
            DobPicker.MaximumDate = DateTime.Today;
            DobPicker.Date = new DateTime(2000, 1, 1);

            UpdateGenderButtons();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_token.Length == 0)
                await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                ShowLoading(true, _isAddMode ? "Preparing form..." : "Loading person...");

                _token = await SecureStorage.GetAsync("api_token") ?? string.Empty;
                var loggedInStr = await SecureStorage.GetAsync("logged_in_memberID") ?? "0";
                _loggedInMemberId = int.TryParse(loggedInStr, out var id) ? id : 0;

                // Load reference data (titles + entity types) in parallel.
                var titlesTask = ApiService.GetTitlesAsync(_token);
                var entityTypesTask = ApiService.GetEntityTypesAsync(_token);
                await Task.WhenAll(titlesTask, entityTypesTask);

                PopulatePicker(TitlePicker, titlesTask.Result);
                PopulatePicker(EntityTypePicker, entityTypesTask.Result);

                // Person Type list isn't exposed by a dedicated endpoint here yet — we
                // reuse entity types as a placeholder so the control is functional.
                PopulatePicker(MemberTypePicker, entityTypesTask.Result);

                // Default entity type to the first option (typically "Individual").
                if (EntityTypePicker.Items.Count > 0 && EntityTypePicker.SelectedIndex < 0)
                    EntityTypePicker.SelectedIndex = 0;

                if (!_isAddMode)
                    await LoadMemberAsync();

                ApplyEntityTypeVisibility();
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async Task LoadMemberAsync()
        {
            var detail = await ApiService.GetMemberAsync(_memberId, _token);
            if (detail == null)
                return;

            FirstNameEntry.Text = detail.FirstName;
            LastNameEntry.Text = detail.LastName;
            EmailEntry.Text = detail.Email;
            MobileEntry.Text = detail.MobilePhone;
            HomeContactEntry.Text = detail.Telephone;
            AddressEntry.Text = detail.Address;
            UserNameEntry.Text = detail.UserName;
            ExternalPasswordEntry.Text = detail.ExternalPassword;
            RsaIdEntry.Text = detail.Rsaid;
            EmpCodeEntry.Text = detail.EmpCode;
            ExternalRefEntry.Text = detail.ExternalRef;

            _sex = (detail.Sex ?? string.Empty).ToUpperInvariant();
            UpdateGenderButtons();

            if (detail.Birthday.HasValue && detail.Birthday.Value > DateTime.MinValue)
            {
                DobPicker.Date = detail.Birthday.Value.Date > DateTime.Today
                    ? DateTime.Today
                    : detail.Birthday.Value.Date;
                _dobSet = true;
            }

            // Leader (pre-selected from the loaded record).
            _selectedLeaderId = detail.LeaderId;
            _selectedLeaderName = detail.Leader ?? string.Empty;
            _suppressLeaderSearch = true;
            LeaderEntry.Text = _selectedLeaderName;
            _suppressLeaderSearch = false;

            SelectPickerByValue(TitlePicker, detail.TitleId?.ToString());
            SelectPickerByValue(EntityTypePicker, detail.EntityTypeId?.ToString());
            SelectPickerByValue(MemberTypePicker, detail.MemberTypeId?.ToString());

            StatusSwitch.IsToggled = detail.MemberStatus == 1;
            UpdateStatusHint();
        }

        // -- Picker helpers --------------------------------------------------

        private static void PopulatePicker(Picker picker, List<DropDownItem> items)
        {
            picker.ItemsSource = items ?? new List<DropDownItem>();
        }

        private static void SelectPickerByValue(Picker picker, string? value)
        {
            if (string.IsNullOrEmpty(value) || picker.ItemsSource == null)
                return;

            for (int i = 0; i < picker.ItemsSource.Count; i++)
            {
                if (picker.ItemsSource[i] is DropDownItem item &&
                    string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase))
                {
                    picker.SelectedIndex = i;
                    return;
                }
            }
        }

        private static DropDownItem? GetSelectedItem(Picker picker) =>
            picker.SelectedIndex >= 0 && picker.ItemsSource != null
                ? picker.ItemsSource[picker.SelectedIndex] as DropDownItem
                : null;

        private static int? GetSelectedIntValue(Picker picker)
        {
            var item = GetSelectedItem(picker);
            return item != null && int.TryParse(item.Value, out var v) ? v : null;
        }

        // -- Entity type (show/hide person-only fields) ----------------------

        private void OnEntityTypeChanged(object sender, EventArgs e) => ApplyEntityTypeVisibility();

        private void ApplyEntityTypeVisibility()
        {
            // Entity type "2" is Company in the web app; everything else is treated
            // as an individual person. Company hides the person-specific fields.
            var value = GetSelectedItem(EntityTypePicker)?.Value;
            bool isCompany = value == "2";

            TitleSection.IsVisible = !isCompany;
            LastNameSection.IsVisible = !isCompany;
            GenderSection.IsVisible = !isCompany;
            HomeContactSection.IsVisible = !isCompany;
            DobSection.IsVisible = !isCompany;
            UserNameSection.IsVisible = !isCompany;
            ExternalPasswordSection.IsVisible = !isCompany;
            RsaIdSection.IsVisible = !isCompany;

            FirstNameLabel.Text = isCompany ? "Company Name *" : "First Name *";
            StatusLabel.Text = isCompany ? "Company Status" : "Person Status";
        }

        // -- Gender ----------------------------------------------------------

        private void OnMaleClicked(object sender, EventArgs e)
        {
            _sex = "M";
            UpdateGenderButtons();
        }

        private void OnFemaleClicked(object sender, EventArgs e)
        {
            _sex = "F";
            UpdateGenderButtons();
        }

        private void UpdateGenderButtons()
        {
            var primary = (Color)Application.Current!.Resources["Primary"];
            var border = (Color)Application.Current!.Resources["Border"];
            var textSecondary = (Color)Application.Current!.Resources["TextSecondary"];

            bool male = _sex == "M";
            bool female = _sex == "F";

            MaleButton.BackgroundColor = male ? primary : Colors.Transparent;
            MaleButton.TextColor = male ? Colors.White : textSecondary;
            MaleButton.BorderColor = male ? primary : border;

            FemaleButton.BackgroundColor = female ? primary : Colors.Transparent;
            FemaleButton.TextColor = female ? Colors.White : textSecondary;
            FemaleButton.BorderColor = female ? primary : border;
        }

        // -- DOB -------------------------------------------------------------

        // (DatePicker has no "empty" state; we only send DOB once the user has set it.)

        // -- Status ----------------------------------------------------------

        private void OnStatusToggled(object sender, ToggledEventArgs e) => UpdateStatusHint();

        private void UpdateStatusHint()
        {
            StatusHintLabel.Text = StatusSwitch.IsToggled ? "Active" : "Inactive";
        }

        // -- Leader autocomplete --------------------------------------------

        private async void OnLeaderTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressLeaderSearch)
                return;

            // Any manual edit invalidates the previously selected leader.
            _selectedLeaderId = null;

            var term = (e.NewTextValue ?? string.Empty).Trim();
            if (term.Length < 3)
            {
                LeaderResults.Clear();
                LeaderSuggestionsBorder.IsVisible = false;
                return;
            }

            _leaderSearchCts?.Cancel();
            _leaderSearchCts?.Dispose();
            _leaderSearchCts = new CancellationTokenSource();
            var ct = _leaderSearchCts.Token;

            try
            {
                await Task.Delay(300, ct);
                if (ct.IsCancellationRequested)
                    return;

                // Use the edited member's id when editing, otherwise the logged-in member.
                int contextMemberId = _isAddMode ? _loggedInMemberId : _memberId;
                var results = await ApiService.SearchLeadersAsync(term, contextMemberId, _token);

                if (ct.IsCancellationRequested)
                    return;

                LeaderResults.Clear();
                foreach (var r in results)
                    LeaderResults.Add(r);

                LeaderSuggestionsBorder.IsVisible = LeaderResults.Count > 0;
            }
            catch (TaskCanceledException)
            {
                // Superseded by a newer keystroke.
            }
        }

        private void OnLeaderSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is not LeaderItem leader)
                return;

            _selectedLeaderId = leader.MemberId;
            _selectedLeaderName = leader.Display;

            _suppressLeaderSearch = true;
            LeaderEntry.Text = leader.Display;
            _suppressLeaderSearch = false;

            LeaderSuggestions.SelectedItem = null;
            LeaderSuggestionsBorder.IsVisible = false;
        }

        // -- Save ------------------------------------------------------------

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var error = Validate(out bool isCompany);
            if (error != null)
            {
                await DisplayAlert("Missing Information", error, "OK");
                return;
            }

            var request = new UpdateMemberRequest
            {
                MemberId = _memberId,
                FirstName = FirstNameEntry.Text?.Trim() ?? string.Empty,
                LastName = isCompany ? string.Empty : (LastNameEntry.Text?.Trim() ?? string.Empty),
                Email = EmailEntry.Text?.Trim() ?? string.Empty,
                Mobile = DigitsOnly(MobileEntry.Text),
                Phone = isCompany ? string.Empty : DigitsOnly(HomeContactEntry.Text),
                Sex = isCompany ? string.Empty : _sex,
                TitleId = isCompany ? null : GetSelectedIntValue(TitlePicker),
                Address = AddressEntry.Text?.Trim() ?? string.Empty,
                Dob = (!isCompany && _dobSet) ? DobPicker.Date : null,
                MemberTypeId = GetSelectedIntValue(MemberTypePicker),
                LeaderId = _selectedLeaderId,
                Rsaid = isCompany ? null : RsaIdEntry.Text?.Trim(),
                UserName = isCompany ? string.Empty : (UserNameEntry.Text?.Trim() ?? string.Empty),
                ExternalPassword = isCompany ? null : ExternalPasswordEntry.Text,
                RoleId = _isAddMode ? GetSelectedIntValue(RolePicker) : null,
                Status = StatusSwitch.IsToggled,
                EmpCode = EmpCodeEntry.Text?.Trim(),
                EntityTypeId = GetSelectedIntValue(EntityTypePicker),
                ExternalReference = ExternalRefEntry.Text?.Trim(),
                MemberTagUid = null
            };

            try
            {
                ShowLoading(true, "Saving...");
                var result = await ApiService.SaveMemberAsync(request, _token);

                if (result.IsSuccess)
                {
                    Saved?.Invoke(this, EventArgs.Empty);
                    await DisplayAlert("Saved",
                        _isAddMode ? "Person added successfully." : "Person updated successfully.", "OK");
                    await Navigation.PopAsync();
                }
                else
                {
                    await DisplayAlert("Could Not Save",
                        result.ErrorMessage ?? "An unknown error occurred.", "OK");
                }
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private string? Validate(out bool isCompany)
        {
            isCompany = GetSelectedItem(EntityTypePicker)?.Value == "2";

            if (GetSelectedItem(EntityTypePicker) == null)
                return "Please select an entity type.";

            if (string.IsNullOrWhiteSpace(FirstNameEntry.Text))
                return isCompany ? "Company name is required." : "First name is required.";

            if (!isCompany && string.IsNullOrWhiteSpace(LastNameEntry.Text))
                return "Last name is required.";

            if (_selectedLeaderId == null || string.IsNullOrWhiteSpace(LeaderEntry.Text))
                return "Please select a leader from the suggestion list.";

            if (string.IsNullOrWhiteSpace(EmailEntry.Text))
                return "Email is required.";

            if (!EmailRegex.IsMatch(EmailEntry.Text.Trim()))
                return "Please enter a valid email address.";

            if (GetSelectedItem(MemberTypePicker) == null)
                return "Person type is required.";

            if (string.IsNullOrWhiteSpace(AddressEntry.Text))
                return "Address is required.";

            if (!isCompany && string.IsNullOrWhiteSpace(_sex))
                return "Please select a gender.";

            if (!isCompany && string.IsNullOrWhiteSpace(UserNameEntry.Text))
                return "Username is required.";

            if (_isAddMode && GetSelectedItem(RolePicker) == null)
                return "Please select a role.";

            return null;
        }

        private static string DigitsOnly(string? value) =>
            string.IsNullOrEmpty(value) ? string.Empty : Regex.Replace(value, "[^0-9]", string.Empty);

        // -- Navigation / overlay -------------------------------------------

        private void ShowLoading(bool show, string message = "Loading...")
        {
            LoadingLabel.Text = message;
            LoadingOverlay.IsVisible = show;
            SaveButton.IsEnabled = !show;
            CancelButton.IsEnabled = !show;
        }

        private async void OnCancelClicked(object sender, EventArgs e) => await Navigation.PopAsync();

        private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();
    }
}
