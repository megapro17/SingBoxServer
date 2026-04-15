using SingBoxServer.Models;

namespace SingBoxServer.Services.SubscriptionLoader;

public interface ISubscriptionLoader
{
    Task<string> LoadContentAsync(ServerSource server, CancellationToken ct = default);
}
