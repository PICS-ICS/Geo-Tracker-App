using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using GeoTrackerApp3.Models;

namespace GeoTrackerApp3.Services
{
    public static class ApiService
    {
        // Replace with your real API base address
        private static readonly string BaseUrl = "http://picsapi.ics.co.za";

        private static readonly HttpClient _httpClient;

        //static ApiService()
        //{
        //    _httpClient.BaseAddress = new Uri(BaseUrl);
        //    _httpClient.DefaultRequestHeaders.Accept.Clear();
        //}

        static ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _httpClient.DefaultRequestHeaders.Accept.Clear();
        }


        public static async Task<ApiResult> RegisterAsync(RegisterRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/Auth/register", request);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
                    if (body != null && !string.IsNullOrEmpty(body.Token))
                    {
                        return ApiResult.Success(body.Token);

                    }
                    return ApiResult.Failure("Invalid response from server.");
                }
                else
                {
                    var msg = await TryGetErrorMessage(response);
                    return ApiResult.Failure(msg ?? $"Server returned {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return ApiResult.Failure(ex.Message);
            }
        }

        public static async Task<ApiResult> LoginAsync(LoginRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/Authentication/login", request);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
                    if (body != null && !string.IsNullOrEmpty(body.Token))
                    {
                        try
                        {
                            await SecureStorage.SetAsync("api_token", body.Token);
                            await SecureStorage.SetAsync("logged_in_memberID", body.MemberID.ToString());
                            await SecureStorage.SetAsync("logged_in_companyID", body.CompanyID.ToString());
                        }
                        catch (Exception ex)
                        {
                            // SecureStorage can throw if device doesn't support it or permissions missing
                            System.Diagnostics.Debug.WriteLine($"SecureStorage error: {ex.Message}");
                        }

                        return ApiResult.Success(body.Token);
                    }
                    return ApiResult.Failure("Invalid response from server.");
                }
                else
                {
                    var msg = await TryGetErrorMessage(response);
                    return ApiResult.Failure(msg ?? $"Server returned {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return ApiResult.Failure(ex.Message);
            }
        }
        public static async Task<ApiResult> SendLocationAsync(LocationData request, string token)
        {
            try
            {
                AddAuthorizationHeader(token);

                var response = await _httpClient.PostAsJsonAsync("/api/Location/PingLocation", request);

                if (response.IsSuccessStatusCode)
                {
                    return ApiResult.Success("Location updated successfully.");
                }
                else
                {
                    var msg = await TryGetErrorMessage(response);
                    return ApiResult.Failure(msg ?? $"Server returned {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return ApiResult.Failure(ex.Message);
            }
        }

        private static async Task<string?> TryGetErrorMessage(HttpResponseMessage response)
        {
            try
            {
                // try read a JSON error message
                var err = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                if (err != null && !string.IsNullOrEmpty(err.Message))
                    return err.Message;
            }
            catch { /* ignore parse errors */ }

            try
            {
                var plain = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(plain))
                    return plain;
            }
            catch { }

            return null;
        }

        // Example helper to add token to a request:
        public static void AddAuthorizationHeader(string token)
        {
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }
    }
}
