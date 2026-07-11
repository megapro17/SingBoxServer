using SingBoxServer.Core.Models;

namespace SingBoxServer.Services.Subscriptions;

internal interface ISubscriptionLoader
{
    Task<string> LoadContentAsync(ServerSource server, CancellationToken ct = default);
}
