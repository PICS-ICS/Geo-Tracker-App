using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GeoTrackerApp3.Models;
using GeoTrackerApp3.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace GeoTrackerApp3.Views
{
    public partial class PeoplePage : ContentPage
    {
        private const int PageSize = 25;

        private string _token = string.Empty;
        private int _memberId;

        // Master list of everything fetched from the server so far.
        private readonly List<Member> _allMembers = new();

        // Server paging state.
        private int _pageNumber = 1;
        private int _totalCount;
        private bool _hasMore = true;

        // Active status filter: null = All, 1 = Active, 0 = Inactive.
        private int? _statusFilter = 1;
        private string _searchText = string.Empty;

        // Debounce for the search box.
        private CancellationTokenSource? _searchDebounceCts;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); } }
        }

        private bool _isLoadingMore;
        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set { if (_isLoadingMore != value) { _isLoadingMore = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<Member> Members { get; } = new();

        public ICommand RefreshCommand { get; }

        public PeoplePage()
        {
            InitializeComponent();
            RefreshCommand = new Command(async () => await LoadFirstPageAsync());
            BindingContext = this;
            UpdateStatusButtons();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadCredentialsAsync();

            // Only auto-load the first time the page appears.
            if (_allMembers.Count == 0 && !IsLoading)
                await LoadFirstPageAsync();
        }

        private async Task LoadCredentialsAsync()
        {
            try
            {
                _token = await SecureStorage.GetAsync("api_token") ?? string.Empty;
                var memberIdStr = await SecureStorage.GetAsync("logged_in_memberID") ?? "0";
                _memberId = int.TryParse(memberIdStr, out var id) ? id : 0;
            }
            catch (Exception ex)
            {
                ErrorDisplayService.ShowError("People", ex);
            }
        }

        private async Task LoadFirstPageAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;

                _pageNumber = 1;
                _hasMore = true;
                _allMembers.Clear();

                var response = await ApiService.GetAdminMemberListAsync(
                    _memberId, sortBy: "firstName", sortDirection: "asc",
                    filterBy: string.Empty, filterValue: string.Empty,
                    pageNumber: _pageNumber, pageSize: PageSize, token: _token);

                _totalCount = response?.TotalCount ?? 0;
                var items = response?.Items ?? new List<Member>();

                _allMembers.AddRange(items);
                _hasMore = _allMembers.Count < _totalCount && items.Count > 0;

                ApplyFilter();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadNextPageAsync()
        {
            if (IsLoading || IsLoadingMore || !_hasMore) return;

            try
            {
                IsLoadingMore = true;
                _pageNumber++;

                var response = await ApiService.GetAdminMemberListAsync(
                    _memberId, sortBy: "firstName", sortDirection: "asc",
                    filterBy: string.Empty, filterValue: string.Empty,
                    pageNumber: _pageNumber, pageSize: PageSize, token: _token);

                var items = response?.Items ?? new List<Member>();
                if (response?.TotalCount > 0)
                    _totalCount = response.TotalCount;

                _allMembers.AddRange(items);
                _hasMore = _allMembers.Count < _totalCount && items.Count > 0;

                ApplyFilter();
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        /// <summary>
        /// Rebuilds the visible <see cref="Members"/> collection from the master list,
        /// applying the current status filter and search text on the client.
        /// </summary>
        private void ApplyFilter()
        {
            IEnumerable<Member> query = _allMembers;

            if (_statusFilter.HasValue)
                query = query.Where(m => m.MemberStatus == _statusFilter.Value);

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var term = _searchText.Trim();
                query = query.Where(m =>
                    Contains(m.FullName, term) ||
                    Contains(m.Email, term) ||
                    Contains(m.MobilePhone, term) ||
                    Contains(m.Address, term) ||
                    Contains(m.UserName, term) ||
                    Contains(m.MemberType, term));
            }

            var filtered = query.ToList();

            Members.Clear();
            foreach (var m in filtered)
                Members.Add(m);

            UpdateCountLabel(filtered.Count);
        }

        private static bool Contains(string? source, string term) =>
            !string.IsNullOrEmpty(source) &&
            source.Contains(term, StringComparison.OrdinalIgnoreCase);

        private void UpdateCountLabel(int shown)
        {
            if (_totalCount > 0)
                CountLabel.Text = $"Showing {shown} of {_totalCount} people";
            else
                CountLabel.Text = $"{shown} people";
        }

        // -- Status filter buttons ------------------------------------------

        private async void OnStatusAllClicked(object sender, EventArgs e)
        {
            _statusFilter = null;
            UpdateStatusButtons();
            await EnsureFilteredResultsAsync();
        }

        private async void OnStatusActiveClicked(object sender, EventArgs e)
        {
            _statusFilter = 1;
            UpdateStatusButtons();
            await EnsureFilteredResultsAsync();
        }

        private async void OnStatusInactiveClicked(object sender, EventArgs e)
        {
            _statusFilter = 0;
            UpdateStatusButtons();
            await EnsureFilteredResultsAsync();
        }

        private void UpdateStatusButtons()
        {
            var activeBg = (Color)Application.Current!.Resources["CardBackground"];
            var activeText = (Color)Application.Current!.Resources["Primary"];
            var inactiveText = (Color)Application.Current!.Resources["TextSecondary"];

            BtnAll.BackgroundColor = _statusFilter == null ? activeBg : Colors.Transparent;
            BtnAll.TextColor = _statusFilter == null ? activeText : inactiveText;

            BtnActive.BackgroundColor = _statusFilter == 1 ? activeBg : Colors.Transparent;
            BtnActive.TextColor = _statusFilter == 1 ? activeText : inactiveText;

            BtnInactive.BackgroundColor = _statusFilter == 0 ? activeBg : Colors.Transparent;
            BtnInactive.TextColor = _statusFilter == 0 ? activeText : inactiveText;
        }

        /// <summary>
        /// Re-applies the filter, and if the current page subset yields no matches but
        /// more server pages exist, keeps loading pages until we have something to show.
        /// </summary>
        private async Task EnsureFilteredResultsAsync()
        {
            ApplyFilter();

            // If filtering hid everything but the server still has more rows, pull them in.
            while (Members.Count == 0 && _hasMore && !IsLoading)
            {
                await LoadNextPageAsync();
            }
        }

        // -- Search ----------------------------------------------------------

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = e.NewTextValue ?? string.Empty;

            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = new CancellationTokenSource();
            var token = _searchDebounceCts.Token;

            try
            {
                await Task.Delay(300, token);
                if (!token.IsCancellationRequested)
                    await EnsureFilteredResultsAsync();
            }
            catch (TaskCanceledException)
            {
                // A newer keystroke superseded this one — ignore.
            }
        }

        // -- Infinite scroll -------------------------------------------------

        private async void OnThresholdReached(object sender, EventArgs e)
        {
            await LoadNextPageAsync();
        }

        // -- Row / toolbar actions ------------------------------------------

        private async void OnMemberTapped(object sender, TappedEventArgs e)
        {
            if (sender is BindableObject bo && bo.BindingContext is Member member)
            {
                var page = new EditPersonPage(member.MemberID);
                page.Saved += OnPersonSaved;
                await Navigation.PushAsync(page);
            }
        }

        private async void OnAddPersonClicked(object sender, EventArgs e)
        {
            var page = new EditPersonPage();
            page.Saved += OnPersonSaved;
            await Navigation.PushAsync(page);
        }

        private void OnPersonSaved(object? sender, EventArgs e)
        {
            // A person was added or edited — refresh the list from the server.
            if (sender is EditPersonPage page)
                page.Saved -= OnPersonSaved;

            MainThread.BeginInvokeOnMainThread(async () => await LoadFirstPageAsync());
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await LoadFirstPageAsync();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}
