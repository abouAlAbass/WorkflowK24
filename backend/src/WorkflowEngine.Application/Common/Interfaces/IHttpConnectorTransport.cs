namespace WorkflowEngine.Application.Common.Interfaces;

/// <summary>
/// Low-level HTTP transport abstraction for the workflow HTTP connector.
/// Implementations MUST disable automatic redirects (AllowAutoRedirect = false)
/// so the caller can revalidate every redirect hop against ISsrfGuard.
/// </summary>
public interface IHttpConnectorTransport
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken ct = default);
}
