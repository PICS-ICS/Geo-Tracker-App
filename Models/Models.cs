using System.Text.Json.Serialization;

namespace GeoTrackerApp3.Models
{
    public class LoginRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

    }

    public class LocationData
    {
        public int memberID { get; set; }
        public int companyID { get; set; }
        public double lat { get; set; }
        public double lon { get; set; }
        public DateTime pingTime { get; set; }
        public string ipAddress { get; set; } = string.Empty;
        public string deviceOS { get; set; } = string.Empty;
        public string deviceModel { get; set; } = string.Empty;
    }


    public class AuthResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("memberId")]
        public int MemberID { get; set; }

        [JsonPropertyName("companyID")]
        public int CompanyID { get; set; }

        [JsonPropertyName("memberGuid")]
        public string MemberGUID { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }

    public class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    public class GeofenceLocation
    {
        [System.Text.Json.Serialization.JsonPropertyName("lat")]
        public double Lat { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lon")]
        public double Lon { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    // Matches the API envelope: { "success": ..., "message": ..., "data": [ ... ] }
    public class GeofenceResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("success")]
        public bool Success { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<GeofenceLocation> Data { get; set; } = new();
    }

    public class ApiResult
    {
        public bool IsSuccess { get; private set; }
        public string? Token { get; private set; }
        public string? ErrorMessage { get; private set; }

        public static ApiResult Success(string token) => new ApiResult { IsSuccess = true, Token = token };
        public static ApiResult Failure(string message) => new ApiResult { IsSuccess = false, ErrorMessage = message };
    }
}
