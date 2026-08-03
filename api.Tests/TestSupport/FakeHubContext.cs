using api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace api.Tests.TestSupport;

/// <summary>
/// PaymentEventNotifier'ın IHubContext&lt;GameHub&gt; bağımlılığı için, gerçek bir
/// SignalR sunucusu kurmadan test edilebilmesini sağlayan minimal sahte
/// implementasyon (mocking kütüphanesi eklemek yerine — YAGNI). Gönderilen
/// event'leri <see cref="Proxy"/> üzerinden gözlemlenebilir kılar.
/// </summary>
public class FakeHubContext : IHubContext<GameHub>
{
    public FakeClientProxy Proxy { get; } = new();

    public IHubClients Clients => new FakeHubClients(Proxy);
    public IGroupManager Groups { get; } = new FakeGroupManager();
}

public class FakeHubClients : IHubClients
{
    private readonly FakeClientProxy _proxy;

    public FakeHubClients(FakeClientProxy proxy)
    {
        _proxy = proxy;
    }

    public IClientProxy All => _proxy;
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;
    public IClientProxy Client(string connectionId) => _proxy;
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;
    public IClientProxy Group(string groupName) => _proxy;
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
    public IClientProxy OthersInGroup(string groupName) => _proxy;
    public IClientProxy User(string userId) => _proxy;
    public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
}

public class FakeClientProxy : IClientProxy
{
    public List<(string Method, object?[] Args)> Sent { get; } = new();

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        Sent.Add((method, args));
        return Task.CompletedTask;
    }
}

public class FakeGroupManager : IGroupManager
{
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
