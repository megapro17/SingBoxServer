using SingBoxServer.Models;

namespace SingBoxServer.Services.ConfigGenerator;

public interface IConfigGenerator<T>
{
    string Name { get; }
    Task<T> GenerateAsync(UserProfile user, Dictionary<string, ServerSource> server, T template);
}