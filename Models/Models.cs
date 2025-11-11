using System.Text.Json.Serialization;

namespace GeoTrackerApp3.Models
{
    public class LoginRequest
    {
        public string UsernameOrEmail { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
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
        public double latitude { get; set; }
        public double longatude { get; set; }
        public DateTime pingTime { get; set; }
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public int MemberID { get; set; }
        public int CompanyID { get; set; }
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

    public class ApiResult
    {
        public bool IsSuccess { get; private set; }
        public string? Token { get; private set; }
        public string? ErrorMessage { get; private set; }

        public static ApiResult Success(string token) => new ApiResult { IsSuccess = true, Token = token };
        public static ApiResult Failure(string message) => new ApiResult { IsSuccess = false, ErrorMessage = message };
    }
}
