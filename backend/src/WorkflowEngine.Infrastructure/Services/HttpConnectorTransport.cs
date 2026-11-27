using WorkflowEngine.Application.Common.Interfaces;

namespace WorkflowEngine.Infrastructure.Services;

/// <summary>
/// Default HTTP transport for HttpNodeExecutor.
/// Explicitly disables automatic redirects (AllowAutoRedirect = false) so every redirect hop
/// is revalidated by ISsrfGuard.
/// </summary>
public sealed class HttpConnectorTransport : IHttpConnectorTransport, IDisposable
{
    private readonly HttpClient _httpClient;

    public HttpConnectorTransport()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 50
        };

        _httpClient = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan // Per-request timeout managed via linked CTS
        };
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
