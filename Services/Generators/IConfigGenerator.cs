using SingBoxServer.Core.Models;

namespace SingBoxServer.Services.Generators;

public interface IConfigGenerator<T>
{
    Task<T> GenerateAsync(UserProfile user);
}
