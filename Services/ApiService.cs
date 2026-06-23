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
        private static string BaseUrl => EnvironmentConfig.ApiBaseUrl;

        // HttpClient that is recreated when environment changes
        private static HttpClient? _httpClient;
        private static readonly object _httpClientLock = new();
        private static HttpClient HttpClient
        {
            get
            {
                if (_httpClient is null)
                {
                    lock (_httpClientLock)
                    {
                        _httpClient ??= CreateHttpClient();
                    }
                }
                return _httpClient;
            }
        }

        static ApiService()
        {
            EnvironmentConfig.OnEnvironmentChanged += InvalidateHttpClient;
        }

        private static void InvalidateHttpClient()
        {
            lock (_httpClientLock)
            {
                _httpClient?.Dispose();
                _httpClient = null;
            }
        }

        // Cache IP address to avoid repeated network calls
        private static string _cachedIpAddress;
        private static DateTime _ipCacheTime = DateTime.MinValue;
        private static readonly TimeSpan IP_CACHE_DURATION = TimeSpan.FromMinutes(5);

        private const int MAX_RETRIES = 3;

        private static HttpClient CreateHttpClient()
        {
            var apiHost = EnvironmentConfig.ApiHost;

#if ANDROID
            var handler = new Xamarin.Android.Net.AndroidMessageHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    // Only bypass validation for our API domain
                    if (message.RequestUri?.Host == apiHost)
                        return true;

                    return errors == System.Net.Security.SslPolicyErrors.None;
                }
            };
#else
            var handler = new HttpClientHandler
            {
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    if (message.RequestUri?.Host == apiHost)
                        return true;
                    return errors == System.Net.Security.SslPolicyErrors.None;
                }
            };
#endif

            return new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };
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
            Exception lastException = null;

            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"LoginAsync: Attempt {attempt} of {MAX_RETRIES}");

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
                        // Don't retry HTTP-level errors (4xx/5xx) — those are real server responses
                        var msg = await TryGetErrorMessage(response);
                        return ApiResult.Failure(msg ?? $"Server returned {(int)response.StatusCode}");
                    }
                }
                catch (TaskCanceledException)
                {
                    lastException = new TimeoutException($"Request timed out (attempt {attempt})");
                }
                catch (HttpRequestException httpEx)
                {
                    lastException = httpEx;
                    System.Diagnostics.Debug.WriteLine($"LoginAsync: Attempt {attempt} failed - {httpEx.GetType().Name}: {httpEx.Message} | Inner: {httpEx.InnerException?.Message}");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    System.Diagnostics.Debug.WriteLine($"LoginAsync: Attempt {attempt} failed - {ex.GetType().Name}: {ex.Message}");
                }

                // Wait before retrying (exponential backoff)
                if (attempt < MAX_RETRIES)
                {
                    await Task.Delay(500 * attempt);
                }
            }

            // Build a detailed error message for the user
            var errorDetail = BuildDetailedError(lastException);
            return ApiResult.Failure(errorDetail);
        }

        private static string BuildDetailedError(Exception ex)
        {
            if (ex == null)
                return "Connection failed after multiple attempts.";

            var inner = ex.InnerException;
            var typeName = ex.GetType().Name;
            var msg = ex.Message;

            if (ex is TimeoutException)
                return $"Could not reach the server after {MAX_RETRIES} attempts (timed out). Please check your internet connection.";

            if (ex is HttpRequestException httpEx)
            {
                var innerMsg = inner?.Message ?? "";
                if (innerMsg.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
                    innerMsg.Contains("TLS", StringComparison.OrdinalIgnoreCase) ||
                    innerMsg.Contains("certificate", StringComparison.OrdinalIgnoreCase))
                {
                    return $"SSL/TLS error connecting to server. Your device may not trust the server certificate.\n\nDetails: {innerMsg}";
                }

                if (msg.Contains("No such host", StringComparison.OrdinalIgnoreCase) ||
                    msg.Contains("Name resolution", StringComparison.OrdinalIgnoreCase))
                {
                    return "Cannot resolve server address. Please check your internet connection and try again.";
                }

                return $"Connection failed after {MAX_RETRIES} attempts.\n\nError: {msg}\n{(inner != null ? $"Detail: {inner.Message}" : "")}";
            }

            return $"Unexpected error after {MAX_RETRIES} attempts.\n\nType: {typeName}\nError: {msg}";
        }
        public static async Task<ApiResult> SendLocationAsync(LocationData request, string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    return ApiResult.Failure("No authentication token provided.");
                }

                // Check connectivity before attempting to send
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    await LocationQueueService.EnqueueAsync(request);
                    return ApiResult.Failure("No internet — location queued for later sync.");
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
                    // Remember the last successfully sent location for the details screen
                    LastLocationService.Record(request.lat, request.lon, request.pingTime);

                    // Connection is back — try syncing any queued pings in the background
                    _ = Task.Run(() => LocationQueueService.SyncAsync(token));
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
                await LocationQueueService.EnqueueAsync(request);
                return ApiResult.Failure("Request timed out — location queued.");
            }
            catch (HttpRequestException)
            {
                await LocationQueueService.EnqueueAsync(request);
                return ApiResult.Failure("Network error — location queued.");
            }
            catch (Exception ex)
            {
                await LocationQueueService.EnqueueAsync(request);
                return ApiResult.Failure(ex.Message);
            }
        }

        /// <summary>
        /// Sends a batch of queued location pings to the API.
        /// NOTE: The batch endpoint does not exist yet — uncomment the real call once created.
        /// </summary>
        public static async Task<ApiResult> SendLocationBatchAsync(List<LocationData> locations, string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                    return ApiResult.Failure("No authentication token provided.");

                 // Wrap in the object structure the API expects, mapping property names to match API schema
                 var payload = new
                 {
                     locationPings = locations.Select(l => new
                     {
                         memberId = l.memberID,
                         lat = l.lat,
                         lon = l.lon,
                         pingTime = l.pingTime,
                         deviceOs = l.deviceOS,
                         deviceModel = l.deviceModel,
                         ipaddress = l.ipAddress
                     }).ToList()
                 };

                 using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/LocationPings/CreateBatch");
                 requestMessage.Headers.Authorization =
                     new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                 requestMessage.Content = JsonContent.Create(payload);
                
                 using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                 var response = await HttpClient.SendAsync(requestMessage, cts.Token);
                
                 if (response.IsSuccessStatusCode)
                     return ApiResult.Success("Batch synced successfully.");
                
                 var msg = await TryGetErrorMessage(response);
                 return ApiResult.Failure(msg ?? $"Server returned {(int)response.StatusCode}");

                //// Temporary: send one by one until batch endpoint exists
                //foreach (var location in locations)
                //{
                //    var result = await SendLocationAsync(location, token);
                //    if (!result.IsSuccess)
                //        return ApiResult.Failure($"Failed during individual sync: {result.ErrorMessage}");
                //}
                //return ApiResult.Success("Batch synced successfully (individual sends).");
            }
            catch (TaskCanceledException)
            {
                return ApiResult.Failure("Batch request timed out");
            }
            catch (Exception ex)
            {
                return ApiResult.Failure(ex.Message);
            }
        }

        /// <summary>
        /// Fetches geofence locations for the specified member from the API.
        /// Returns up to 20 lat/lon points that define 500m-radius geofence circles.
        /// </summary>
        public static async Task<List<GeofenceLocation>> GetGeofencesAsync(int memberId, double lat, double lon, string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                    return new List<GeofenceLocation>();

                var url = $"/api/LocationPings/Getgeofence?memberId={memberId}&lat={lat}&lon={lon}";
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
                requestMessage.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var response = await HttpClient.SendAsync(requestMessage, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cts.Token);
                    // The API wraps the points in an envelope: { success, message, data: [ ... ] }
                    var wrapper = JsonSerializer.Deserialize<GeofenceResponse>(json, AppJsonContext.Default.Options);
                    if (wrapper?.Data?.Count > 0)
                        return wrapper.Data;

                    // Fallback: try parsing as a direct array of locations
                    var directList = JsonSerializer.Deserialize<List<GeofenceLocation>>(json, AppJsonContext.Default.Options);
                    return directList ?? new List<GeofenceLocation>();
                }

                System.Diagnostics.Debug.WriteLine($"[Geofence API] Server returned {(int)response.StatusCode}");
                return new List<GeofenceLocation>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Geofence API] Error: {ex.Message}");
                return new List<GeofenceLocation>();
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
