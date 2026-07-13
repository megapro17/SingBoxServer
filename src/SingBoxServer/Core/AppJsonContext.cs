using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using SingBoxServer.Core.Models;
using SingBoxServer.Services.Generators.SingBox;

namespace SingBoxServer.Core;

internal sealed record SuccessResponse(bool Success);
internal sealed record MessageResponse(string Message);

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    // Вот эта магия заменит удаленный JsonStringEnumConverter:
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(UserSettings))]
[JsonSerializable(typeof(SingBoxTemplate))]
[JsonSerializable(typeof(OutboundNode))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(SuccessResponse))]
[JsonSerializable(typeof(MessageResponse))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
