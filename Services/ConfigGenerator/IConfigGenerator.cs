using SingBoxServer.Models;

namespace SingBoxServer.Services.ConfigGenerator;

public interface IConfigGenerator<T>
{
    Task<T> GenerateAsync(UserProfile user);
}
