using SingBoxServer.Core.Models;

namespace SingBoxServer.Services.Generators;

internal interface IConfigGenerator<T>
{
    Task<T> GenerateAsync(UserProfile user, string? device = null);
}
