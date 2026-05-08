using SingBoxServer.Core.Models;

namespace SingBoxServer.Services.Subscriptions;

public interface ISubscriptionLoader
{
    Task<string> LoadContentAsync(ServerSource server, CancellationToken ct = default);
}
