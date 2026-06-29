using System.Text.Json.Serialization;

namespace GeoTrackerApp3.Models;

[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(AuthResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(LocationData))]
[JsonSerializable(typeof(GeofenceLocation))]
[JsonSerializable(typeof(GeofenceResponse))]
[JsonSerializable(typeof(Member))]
[JsonSerializable(typeof(MemberListResponse))]
[JsonSerializable(typeof(MemberDetail))]
[JsonSerializable(typeof(DropDownItem))]
[JsonSerializable(typeof(DropDownResponse))]
[JsonSerializable(typeof(List<DropDownItem>))]
[JsonSerializable(typeof(LeaderItem))]
[JsonSerializable(typeof(List<LeaderItem>))]
[JsonSerializable(typeof(UpdateMemberRequest))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class AppJsonContext : JsonSerializerContext
{
}
