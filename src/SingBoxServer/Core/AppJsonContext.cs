using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using SingBoxServer.Core.Models;
using SingBoxServer.Services.Generators.SingBox;

namespace SingBoxServer.Core;

internal sealed record SuccessResponse(bool Success);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(UserSettings))]
[JsonSerializable(typeof(SingBoxTemplate))]
[JsonSerializable(typeof(OutboundNode))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(SuccessResponse))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
