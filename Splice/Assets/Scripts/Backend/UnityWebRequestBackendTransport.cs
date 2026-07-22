using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Splice.Backend
{
    public interface IBackendAccessTokenProvider
    {
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
    }

    public sealed class StaticBackendAccessTokenProvider : IBackendAccessTokenProvider
    {
        private readonly string token;

        public StaticBackendAccessTokenProvider(string tokenValue) =>
            token = string.IsNullOrWhiteSpace(tokenValue)
                ? throw new ArgumentException("Backend access token is required.", nameof(tokenValue))
                : tokenValue.Trim();

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(token);
        }
    }

    // Player-client HTTP boundary. The token is requested per call so a production auth provider can refresh it;
    // local development uses StaticBackendAccessTokenProvider with the development-only dev:<uuid> bearer.
    public sealed class UnityWebRequestBackendTransport : IBackendTransport
    {
        private readonly Uri baseUri;
        private readonly IBackendAccessTokenProvider tokenProvider;
        private readonly int timeoutSeconds;

        public UnityWebRequestBackendTransport(string baseUrl,
            IBackendAccessTokenProvider accessTokenProvider, int requestTimeoutSeconds = 15)
        {
            baseUri = ValidateBaseUri(baseUrl);
            tokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
            timeoutSeconds = Math.Max(1, requestTimeoutSeconds);
        }

        public async Task<BackendTransportResponse> SendAsync(BackendTransportRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
                return TransportError(BackendErrorCodes.InvalidTransportRequest,
                    "Backend transport request is missing.", string.Empty, false);

            var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
            if (request.requiresAuthentication && string.IsNullOrWhiteSpace(token))
                return TransportError(BackendErrorCodes.ClientAuthorityForbidden,
                    "Backend authentication token is unavailable.", request.requestId, false);

            var endpoint = new Uri(baseUri, request.path);
            using var webRequest = new UnityWebRequest(endpoint, request.method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSeconds,
            };
            if (!string.IsNullOrWhiteSpace(request.bodyJson) && request.method != BackendHttpMethods.Get)
            {
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.bodyJson));
                webRequest.SetRequestHeader("Content-Type", "application/json");
            }
            webRequest.SetRequestHeader("Accept", "application/json");
            webRequest.SetRequestHeader("X-Request-Id", request.requestId);
            if (request.requiresAuthentication)
                webRequest.SetRequestHeader("Authorization", "Bearer " + token);
            if (request.requiresIdempotency)
                webRequest.SetRequestHeader("Idempotency-Key", request.idempotencyKey);

            var operation = webRequest.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    webRequest.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }

            var body = webRequest.downloadHandler?.text ?? string.Empty;
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.responseCode <= 0)
                return TransportError(BackendErrorCodes.ServiceUnavailable,
                    string.IsNullOrWhiteSpace(webRequest.error)
                        ? "Backend connection failed."
                        : webRequest.error,
                    request.requestId, true, body);
            return new BackendTransportResponse
            {
                statusCode = (int)webRequest.responseCode,
                bodyJson = body,
            };
        }

        public static Uri ValidateBaseUri(string baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("Backend base URL must be an absolute HTTP(S) origin.", nameof(baseUrl));
            if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
                throw new ArgumentException("Plain HTTP is allowed only for a loopback development server.",
                    nameof(baseUrl));
            return new Uri(uri.GetLeftPart(UriPartial.Authority) + "/", UriKind.Absolute);
        }

        private static BackendTransportResponse TransportError(string code, string message,
            string requestId, bool retryable, string body = "")
        {
            var error = new BackendErrorDetails
            {
                code = code,
                message = message,
                requestId = requestId ?? string.Empty,
                retryable = retryable,
            };
            return new BackendTransportResponse
            {
                statusCode = 0,
                bodyJson = body ?? string.Empty,
                error = error,
            };
        }
    }
}
