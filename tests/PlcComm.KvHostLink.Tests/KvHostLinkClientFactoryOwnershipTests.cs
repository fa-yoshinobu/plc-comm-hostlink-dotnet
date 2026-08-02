using System.Net.Sockets;

namespace PlcComm.KvHostLink.Tests;

public sealed class KvHostLinkClientFactoryOwnershipTests
{
    private const string TestProfile = "keyence:kv-x500";

    [Fact]
    public async Task SuccessReturnsCreatedInstanceWithoutFactoryDisposal()
    {
        KvHostLinkClient created = CreateClient();
        int openCount = 0;
        int disposeCount = 0;

        KvHostLinkClient returned = await OpenWithDelegates(
            created,
            (_, _) =>
            {
                openCount++;
                return Task.CompletedTask;
            },
            client =>
            {
                Assert.Same(created, client);
                disposeCount++;
                return ValueTask.CompletedTask;
            });

        Assert.Same(created, returned);
        Assert.Equal(1, openCount);
        Assert.Equal(0, disposeCount);
        await returned.DisposeAsync();
    }

    [Fact]
    public Task ConnectionRefusalRethrowsSameInstanceAndDisposesExactlyOnce()
        => AssertFailureOwnershipAsync(new HostLinkConnectionError(
            "injected connection refusal",
            new SocketException((int)SocketError.ConnectionRefused)));

    [Fact]
    public Task DnsFailureRethrowsSameInstanceAndDisposesExactlyOnce()
        => AssertFailureOwnershipAsync(new HostLinkConnectionError(
            "injected DNS failure",
            new SocketException((int)SocketError.HostNotFound)));

    [Fact]
    public Task TimeoutRethrowsSameInstanceAndDisposesExactlyOnce()
        => AssertFailureOwnershipAsync(new HostLinkTimeoutError("injected timeout"));

    [Fact]
    public Task CallerCancellationRethrowsSameInstanceAndDisposesExactlyOnce()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        return AssertFailureOwnershipAsync(
            new OperationCanceledException("injected caller cancellation", null, cancellation.Token),
            cancellation.Token);
    }

    [Fact]
    public async Task DisposalFailureCannotReplaceOriginalOpenFailure()
    {
        KvHostLinkClient created = CreateClient();
        var openFailure = new HostLinkConnectionError("injected open failure");
        var disposalFailure = new InvalidOperationException("injected disposal failure");
        int disposeCount = 0;

        Exception observed = await Assert.ThrowsAsync<HostLinkConnectionError>(() => OpenWithDelegates(
            created,
            (_, _) => Task.FromException(openFailure),
            client =>
            {
                Assert.Same(created, client);
                disposeCount++;
                return ValueTask.FromException(disposalFailure);
            }));

        Assert.Same(openFailure, observed);
        Assert.Equal(1, disposeCount);
        await created.DisposeAsync();
    }

    [Fact]
    public async Task RepeatedFailuresDisposeEachCreatedInstanceExactlyOnce()
    {
        var created = new List<KvHostLinkClient>();
        var disposeCounts = new Dictionary<KvHostLinkClient, int>(ReferenceEqualityComparer.Instance);
        var failures = Enumerable.Range(0, 3)
            .Select(index => new HostLinkConnectionError($"injected failure {index}"))
            .ToArray();

        foreach (HostLinkConnectionError failure in failures)
        {
            Exception observed = await Assert.ThrowsAsync<HostLinkConnectionError>(() =>
                KvHostLinkClientFactory.OpenAndConnectOwnedAsync(
                    Options(),
                    _ =>
                    {
                        KvHostLinkClient client = CreateClient();
                        created.Add(client);
                        return client;
                    },
                    (_, _) => Task.FromException(failure),
                    client =>
                    {
                        disposeCounts[client] = disposeCounts.GetValueOrDefault(client) + 1;
                        return client.DisposeAsync();
                    },
                    CancellationToken.None));
            Assert.Same(failure, observed);
        }

        Assert.Equal(3, created.Count);
        Assert.Equal(3, disposeCounts.Count);
        Assert.All(created, client => Assert.Equal(1, disposeCounts[client]));
    }

    private static async Task AssertFailureOwnershipAsync(
        Exception failure,
        CancellationToken cancellationToken = default)
    {
        KvHostLinkClient created = CreateClient();
        int disposeCount = 0;

        Exception observed = await Assert.ThrowsAnyAsync<Exception>(() => OpenWithDelegates(
            created,
            (_, token) =>
            {
                Assert.Equal(cancellationToken, token);
                return Task.FromException(failure);
            },
            client =>
            {
                Assert.Same(created, client);
                disposeCount++;
                return client.DisposeAsync();
            },
            cancellationToken));

        Assert.Same(failure, observed);
        Assert.Equal(1, disposeCount);
    }

    private static Task<KvHostLinkClient> OpenWithDelegates(
        KvHostLinkClient created,
        Func<KvHostLinkClient, CancellationToken, Task> openClient,
        Func<KvHostLinkClient, ValueTask> disposeClient,
        CancellationToken cancellationToken = default)
        => KvHostLinkClientFactory.OpenAndConnectOwnedAsync(
            Options(),
            options =>
            {
                Assert.Equal(TestProfile, options.PlcProfile);
                return created;
            },
            openClient,
            disposeClient,
            cancellationToken);

    private static KvHostLinkConnectionOptions Options() => new(
        "127.0.0.1",
        8501,
        HostLinkTransportMode.Tcp,
        TestProfile);

    private static KvHostLinkClient CreateClient() => new(
        "127.0.0.1",
        8501,
        HostLinkTransportMode.Tcp,
        TestProfile);
}
