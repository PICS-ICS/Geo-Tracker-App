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

    /// <summary>
    /// A single person returned by GET /api/Member/GetAdminMemberList.
    /// </summary>
    public class Member
    {
        [JsonPropertyName("memberId")]
        public int MemberID { get; set; }

        [JsonPropertyName("leaderId")]
        public int? LeaderID { get; set; }

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("lastName")]
        public string LastName { get; set; } = string.Empty;

        [JsonPropertyName("memberLeaderName")]
        public string MemberLeaderName { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("mobilePhone")]
        public string MobilePhone { get; set; } = string.Empty;

        [JsonPropertyName("memberType")]
        public string MemberType { get; set; } = string.Empty;

        [JsonPropertyName("memberStatusInt")]
        public int MemberStatusInt { get; set; }

        [JsonPropertyName("taskDate")]
        public DateTime? TaskDate { get; set; }

        [JsonPropertyName("age")]
        public string Age { get; set; } = string.Empty;

        [JsonPropertyName("firstVisit")]
        public DateTime? FirstVisit { get; set; }

        [JsonPropertyName("eventType")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("memberLevel")]
        public int MemberLevel { get; set; }

        [JsonPropertyName("memberStatus")]
        public int MemberStatus { get; set; }

        [JsonPropertyName("idNumber")]
        public string? IdNumber { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        [JsonPropertyName("roleID")]
        public int RoleID { get; set; }

        [JsonPropertyName("recordCount")]
        public int RecordCount { get; set; }

        // -- Display helpers (ignored during (de)serialization) --------------

        [JsonIgnore]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [JsonIgnore]
        public bool IsActive => MemberStatus == 1;

        [JsonIgnore]
        public string StatusText => IsActive ? "Active" : "Inactive";

        [JsonIgnore]
        public string DisplayType => string.IsNullOrWhiteSpace(MemberType) ? "Person" : MemberType;

        [JsonIgnore]
        public bool HasEmail => !string.IsNullOrWhiteSpace(Email);

        [JsonIgnore]
        public bool HasPhone => !string.IsNullOrWhiteSpace(MobilePhone) && MobilePhone.Trim('0').Length > 0;

        [JsonIgnore]
        public bool HasAddress => !string.IsNullOrWhiteSpace(Address);

        [JsonIgnore]
        public string AgeText => string.IsNullOrWhiteSpace(Age) ? string.Empty : $"Age {Age}";

        [JsonIgnore]
        public string Initials
        {
            get
            {
                var first = string.IsNullOrEmpty(FirstName) ? string.Empty : FirstName.Substring(0, 1);
                var last = string.IsNullOrEmpty(LastName) ? string.Empty : LastName.Substring(0, 1);
                var initials = $"{first}{last}".ToUpperInvariant();
                return string.IsNullOrEmpty(initials) ? "?" : initials;
            }
        }

        [JsonIgnore]
        public string LastContactedText =>
            TaskDate.HasValue && TaskDate.Value > DateTime.MinValue
                ? $"Last contacted {TaskDate.Value:dd MMM yyyy}"
                : "Not yet contacted";
    }

    /// <summary>
    /// Paged envelope returned by GET /api/Member/GetAdminMemberList.
    /// </summary>
    public class MemberListResponse
    {
        [JsonPropertyName("items")]
        public List<Member> Items { get; set; } = new();

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Full person detail returned by GET /api/Member/Get?id={memberId}.
    /// Used to populate the Add/Edit Person form.
    /// </summary>
    public class MemberDetail
    {
        [JsonPropertyName("memberId")]
        public int MemberId { get; set; }

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("lastName")]
        public string LastName { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("mobilePhone")]
        public string MobilePhone { get; set; } = string.Empty;

        [JsonPropertyName("telephone")]
        public string Telephone { get; set; } = string.Empty;

        [JsonPropertyName("birthday")]
        public DateTime? Birthday { get; set; }

        [JsonPropertyName("titleId")]
        public int? TitleId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("memberType")]
        public string MemberType { get; set; } = string.Empty;

        [JsonPropertyName("memberTypeId")]
        public int? MemberTypeId { get; set; }

        [JsonPropertyName("lat")]
        public double? Lat { get; set; }

        [JsonPropertyName("lon")]
        public double? Lon { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("roleId")]
        public int? RoleId { get; set; }

        [JsonPropertyName("leader")]
        public string Leader { get; set; } = string.Empty;

        [JsonPropertyName("leaderId")]
        public int? LeaderId { get; set; }

        [JsonPropertyName("sex")]
        public string Sex { get; set; } = string.Empty;

        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        [JsonPropertyName("externalPassword")]
        public string? ExternalPassword { get; set; }

        [JsonPropertyName("memberStatus")]
        public int MemberStatus { get; set; }

        [JsonPropertyName("age")]
        public int Age { get; set; }

        [JsonPropertyName("rsaid")]
        public string? Rsaid { get; set; }

        [JsonPropertyName("memberTagUid")]
        public string? MemberTagUid { get; set; }

        [JsonPropertyName("empCode")]
        public string? EmpCode { get; set; }

        [JsonPropertyName("entityTypeId")]
        public int? EntityTypeId { get; set; }

        [JsonPropertyName("memberGuid")]
        public string? MemberGuid { get; set; }

        [JsonPropertyName("externalRef")]
        public string? ExternalRef { get; set; }

        [JsonPropertyName("postalCode")]
        public string? PostalCode { get; set; }
    }

    /// <summary>
    /// Generic { value, text } dropdown item used by the GeneralRead endpoints
    /// (titles, entity types, etc.).
    /// </summary>
    public class DropDownItem
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        public override string ToString() => Text;
    }

    /// <summary>
    /// Envelope returned by the GeneralRead dropdown endpoints:
    /// { "success": ..., "message": ..., "data": [ { value, text } ] }.
    /// </summary>
    public class DropDownResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public List<DropDownItem> Data { get; set; } = new();
    }

    /// <summary>
    /// A selectable leader returned by
    /// GET /api/GeneralRead/companymemberlistselectableasleader.
    /// </summary>
    public class LeaderItem
    {
        [JsonPropertyName("memberId")]
        public int MemberId { get; set; }

        [JsonPropertyName("memberName")]
        public string MemberName { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonIgnore]
        public string Display =>
            !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName
            : !string.IsNullOrWhiteSpace(MemberName) ? MemberName
            : $"Member {MemberId}";
    }

    /// <summary>
    /// Payload sent when creating or updating a person.
    /// Mirrors the fields posted by the web UpdateMemberDetailOnTask action.
    /// </summary>
    public class UpdateMemberRequest
    {
        [JsonPropertyName("memberId")]
        public int MemberId { get; set; }

        [JsonPropertyName("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("lastName")]
        public string LastName { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("mobile")]
        public string Mobile { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = string.Empty;

        [JsonPropertyName("sex")]
        public string Sex { get; set; } = string.Empty;

        [JsonPropertyName("titleId")]
        public int? TitleId { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("dob")]
        public DateTime? Dob { get; set; }

        [JsonPropertyName("memberTypeId")]
        public int? MemberTypeId { get; set; }

        [JsonPropertyName("leaderId")]
        public int? LeaderId { get; set; }

        [JsonPropertyName("rsaid")]
        public string? Rsaid { get; set; }

        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        [JsonPropertyName("externalPassword")]
        public string? ExternalPassword { get; set; }

        [JsonPropertyName("roleId")]
        public int? RoleId { get; set; }

        [JsonPropertyName("status")]
        public bool Status { get; set; }

        [JsonPropertyName("empCode")]
        public string? EmpCode { get; set; }

        [JsonPropertyName("entityTypeId")]
        public int? EntityTypeId { get; set; }

        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }

        [JsonPropertyName("memberTagUid")]
        public string? MemberTagUid { get; set; }
    }
}
