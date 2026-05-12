using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using GeoTrackerApp3.Models;

namespace GeoTrackerApp3.Services
{
    public static class ApiService
    {
        private static readonly string BaseUrl = "https://picsapiconfig.ics.co.za";
        
        // Lazy initialization - only create HttpClient when needed
        private static readonly Lazy<HttpClient> _lazyHttpClient = new Lazy<HttpClient>(CreateHttpClient);
        private static HttpClient HttpClient => _lazyHttpClient.Value;

        // Cache IP address to avoid repeated network calls
        private static string _cachedIpAddress;
        private static DateTime _ipCacheTime = DateTime.MinValue;
        private static readonly TimeSpan IP_CACHE_DURATION = TimeSpan.FromMinutes(5);

        private static HttpClient CreateHttpClient()
        {
#if DEBUG && ANDROID
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            return new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(15) // Reduced from 30 for faster failures
            };
#else
            return new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };
#endif
        }

        public static async Task<string> GetPublicIpAddressAsync()
        {
            try
            {
                // Return cached IP if still valid
                if (!string.IsNullOrEmpty(_cachedIpAddress) && 
                    DateTime.UtcNow - _ipCacheTime < IP_CACHE_DURATION)
                {
                    return _cachedIpAddress;
                }

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                _cachedIpAddress = await client.GetStringAsync("https://api.ipify.org");
                _ipCacheTime = DateTime.UtcNow;
                return _cachedIpAddress;
            }
            catch
            {
                // Return cached value or Unknown
                return _cachedIpAddress ?? "Unknown";
            }
        }



        public static async Task<ApiResult> RegisterAsync(RegisterRequest request)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var response = await HttpClient.PostAsJsonAsync("/api/Auth/register", request, AppJsonContext.Default.RegisterRequest, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AuthResponse, cancellationToken: cts.Token);
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
            catch (TaskCanceledException)
            {
                return ApiResult.Failure("Request timed out");
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
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var response = await HttpClient.PostAsJsonAsync("/api/Authentication/login", request, AppJsonContext.Default.LoginRequest, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AuthResponse, cancellationToken: cts.Token);
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
            catch (TaskCanceledException)
            {
                return ApiResult.Failure("Request timed out");
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
                if (string.IsNullOrEmpty(token))
                {
                    return ApiResult.Failure("No authentication token provided.");
                }

                request.ipAddress = await GetPublicIpAddressAsync();

                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/LocationPings/Create");
                requestMessage.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                requestMessage.Content = JsonContent.Create(request, AppJsonContext.Default.LocationData);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await HttpClient.SendAsync(requestMessage, cts.Token);

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
            catch (TaskCanceledException)
            {
                return ApiResult.Failure("Request timed out");
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
                var err = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.ErrorResponse);
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
    }
}
