using System.Net.Http.Headers;

namespace AdeptTools.Backend.Http.Handlers;

/// <summary>
/// Injects the current Bearer token into every outgoing HTTP request.
/// The token is read from the provider delegate at request time, so token
/// updates (login, refresh, logout) are automatically applied without
/// requiring the typed client to be re-resolved from DI.
/// </summary>
public class BearerTokenHandler : DelegatingHandler
{
    private readonly Func<string?> _tokenProvider;

    public BearerTokenHandler(Func<string?> tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = _tokenProvider();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return base.SendAsync(request, cancellationToken);
    }
}
