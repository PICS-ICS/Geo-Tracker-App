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
