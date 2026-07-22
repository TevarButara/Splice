using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Splice.Backend
{
    public static class BackendHttpMethods
    {
        public const string Get = "GET";
        public const string Post = "POST";
        public const string Put = "PUT";
        public const string Delete = "DELETE";
    }

    public static class BackendErrorCodes
    {
        public const string InvalidTransportRequest = "INVALID_TRANSPORT_REQUEST";
        public const string IdempotencyKeyRequired = "IDEMPOTENCY_KEY_REQUIRED";
        public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
        public const string SerializationRetryRequired = "SERIALIZATION_RETRY_REQUIRED";
        public const string RequestInProgress = "REQUEST_IN_PROGRESS";
        public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
        public const string ClientAuthorityForbidden = "CLIENT_AUTHORITY_FORBIDDEN";
        public const string TransportRouteMissing = "TRANSPORT_ROUTE_MISSING";
    }

    [Serializable]
    public sealed class BackendErrorDetails
    {
        public string code;
        public string message;
        public string requestId;
        public bool retryable;
    }

    [Serializable]
    public sealed class BackendErrorEnvelope
    {
        public BackendErrorDetails error;
    }

    [Serializable]
    public sealed class BackendAck
    {
        public bool success = true;
    }

    public sealed class BackendTransportRequest
    {
        public string method;
        public string path;
        public string requestId;
        public string idempotencyKey;
        public string bodyJson;
        public bool requiresAuthentication = true;
        public bool requiresIdempotency;
    }

    public sealed class BackendTransportResponse
    {
        public int statusCode;
        public string bodyJson;
        public BackendErrorDetails error;

        public bool IsSuccess => statusCode >= 200 && statusCode <= 299 && error == null;
    }

    public interface IBackendTransport
    {
        Task<BackendTransportResponse> SendAsync(BackendTransportRequest request,
            CancellationToken cancellationToken);
    }

    public interface IBackendJsonSerializer
    {
        string ToJson<T>(T value) where T : class;
        T FromJson<T>(string json) where T : class;
    }

    public sealed class UnityBackendJsonSerializer : IBackendJsonSerializer
    {
        public static readonly UnityBackendJsonSerializer Instance = new();

        public string ToJson<T>(T value) where T : class =>
            value == null ? "{}" : JsonUtility.ToJson(value);

        public T FromJson<T>(string json) where T : class =>
            string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<T>(json);
    }

    public sealed class BackendServiceException : Exception
    {
        public int StatusCode { get; }
        public string Code { get; }
        public string RequestId { get; }
        public bool Retryable { get; }

        public BackendServiceException(int statusCode, string code, string message,
            string requestId, bool retryable) : base(message)
        {
            StatusCode = statusCode;
            Code = string.IsNullOrWhiteSpace(code) ? BackendErrorCodes.InvalidTransportRequest : code;
            RequestId = requestId ?? string.Empty;
            Retryable = retryable;
        }
    }

    public static class BackendPayloadHash
    {
        public static string ComputeObject<T>(T value) where T : class =>
            ComputeJson(UnityBackendJsonSerializer.Instance.ToJson(value));

        public static string ComputeJson(string json)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json ?? string.Empty));
            var result = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++) result.Append(bytes[i].ToString("x2"));
            return result.ToString();
        }
    }

    public static class BackendErrorPolicy
    {
        public static bool CanRetry(BackendTransportRequest request, BackendServiceException exception)
        {
            if (request == null || exception == null || !exception.Retryable) return false;
            return !request.requiresIdempotency || !string.IsNullOrWhiteSpace(request.idempotencyKey);
        }

        public static bool IsKnownRetryableCode(string code) =>
            code == BackendErrorCodes.SerializationRetryRequired ||
            code == BackendErrorCodes.RequestInProgress ||
            code == BackendErrorCodes.ServiceUnavailable;
    }

    // Public Meta API client boundary for a Unity player client. Internal raid-result routes are deliberately
    // rejected here; they belong to a trusted Raid Server identity and a different process in C4.
    public sealed class BackendApiClient
    {
        private readonly IBackendTransport transport;
        private readonly IBackendJsonSerializer serializer;

        public BackendApiClient(IBackendTransport transport, IBackendJsonSerializer serializer = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.serializer = serializer ?? UnityBackendJsonSerializer.Instance;
        }

        public Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken)
            where TResponse : class => SendAsync<object, TResponse>(BackendHttpMethods.Get, path,
                null, string.Empty, false, cancellationToken);

        public async Task<TResponse> SendAsync<TRequest, TResponse>(string method, string path,
            TRequest body, string idempotencyKey, bool requiresIdempotency,
            CancellationToken cancellationToken)
            where TRequest : class
            where TResponse : class
        {
            var request = CreateRequest(method, path,
                body == null ? string.Empty : serializer.ToJson(body),
                idempotencyKey, requiresIdempotency);
            var response = await transport.SendAsync(request, cancellationToken);
            if (response == null)
                throw new BackendServiceException(0, BackendErrorCodes.ServiceUnavailable,
                    "Backend transport returned no response.", request.requestId, true);
            if (!response.IsSuccess) throw ToException(response, request.requestId);
            if (typeof(TResponse) == typeof(BackendAck) && string.IsNullOrWhiteSpace(response.bodyJson))
                return new BackendAck() as TResponse;
            var result = serializer.FromJson<TResponse>(response.bodyJson);
            if (result == null)
                throw new BackendServiceException(response.statusCode,
                    BackendErrorCodes.InvalidTransportRequest,
                    "Backend response body could not be deserialized.", request.requestId, false);
            return result;
        }

        public static BackendTransportRequest CreateRequest(string method, string path, string bodyJson,
            string idempotencyKey, bool requiresIdempotency)
        {
            var normalizedMethod = (method ?? string.Empty).Trim().ToUpperInvariant();
            if (normalizedMethod != BackendHttpMethods.Get && normalizedMethod != BackendHttpMethods.Post &&
                normalizedMethod != BackendHttpMethods.Put && normalizedMethod != BackendHttpMethods.Delete)
                throw new BackendServiceException(0, BackendErrorCodes.InvalidTransportRequest,
                    "Unsupported backend HTTP method.", string.Empty, false);
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/v1/", StringComparison.Ordinal) ||
                path.StartsWith("/internal/", StringComparison.OrdinalIgnoreCase))
                throw new BackendServiceException(0, BackendErrorCodes.ClientAuthorityForbidden,
                    "Unity player clients may call only public /v1 routes.", string.Empty, false);
            if (requiresIdempotency && string.IsNullOrWhiteSpace(idempotencyKey))
                throw new BackendServiceException(0, BackendErrorCodes.IdempotencyKeyRequired,
                    "Idempotency-Key is required for this operation.", string.Empty, false);

            return new BackendTransportRequest
            {
                method = normalizedMethod,
                path = path,
                requestId = Guid.NewGuid().ToString("N"),
                idempotencyKey = idempotencyKey ?? string.Empty,
                bodyJson = bodyJson ?? string.Empty,
                requiresAuthentication = true,
                requiresIdempotency = requiresIdempotency,
            };
        }

        private BackendServiceException ToException(BackendTransportResponse response, string fallbackRequestId)
        {
            var error = response.error;
            if (error == null && !string.IsNullOrWhiteSpace(response.bodyJson))
            {
                try { error = serializer.FromJson<BackendErrorEnvelope>(response.bodyJson)?.error; }
                catch (Exception) { /* Preserve a stable fallback error below. */ }
            }
            var code = error?.code ?? BackendErrorCodes.InvalidTransportRequest;
            var retryable = error?.retryable == true || BackendErrorPolicy.IsKnownRetryableCode(code);
            return new BackendServiceException(response.statusCode, code,
                error?.message ?? "Backend request failed.",
                error?.requestId ?? fallbackRequestId, retryable);
        }
    }

    // Deterministic in-memory transport for C0 tests. It models server idempotency semantics without opening
    // a socket or creating a cloud resource, and can be replaced by an HTTP transport after C1-C3 exist.
    public sealed class LoopbackBackendTransport : IBackendTransport
    {
        private sealed class IdempotentResponse
        {
            public string requestHash;
            public BackendTransportResponse response;
        }

        private readonly Dictionary<string, Func<BackendTransportRequest, CancellationToken,
            Task<BackendTransportResponse>>> routes = new();
        private readonly Dictionary<string, IdempotentResponse> idempotency = new();

        public int HandlerCalls { get; private set; }

        public void Register(string method, string path,
            Func<BackendTransportRequest, CancellationToken, Task<BackendTransportResponse>> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            routes[RouteKey(method, path)] = handler;
        }

        public async Task<BackendTransportResponse> SendAsync(BackendTransportRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null) return Error(400, BackendErrorCodes.InvalidTransportRequest,
                "Transport request is missing.", string.Empty, false);

            var replayScope = string.Empty;
            var requestHash = string.Empty;
            if (request.requiresIdempotency)
            {
                if (string.IsNullOrWhiteSpace(request.idempotencyKey))
                    return Error(400, BackendErrorCodes.IdempotencyKeyRequired,
                        "Idempotency-Key is required.", request.requestId, false);
                replayScope = RouteKey(request.method, request.path) + "|" + request.idempotencyKey;
                requestHash = BackendPayloadHash.ComputeJson(request.bodyJson);
                if (idempotency.TryGetValue(replayScope, out var replay))
                    return replay.requestHash == requestHash
                        ? replay.response
                        : Error(409, BackendErrorCodes.IdempotencyKeyReused,
                            "Idempotency key was reused with a different request body.",
                            request.requestId, false);
            }

            if (!routes.TryGetValue(RouteKey(request.method, request.path), out var handler))
                return Error(404, BackendErrorCodes.TransportRouteMissing,
                    "No loopback route is registered for this request.", request.requestId, false);

            HandlerCalls++;
            var response = await handler(request, cancellationToken);
            if (request.requiresIdempotency && response?.IsSuccess == true)
                idempotency[replayScope] = new IdempotentResponse
                {
                    requestHash = requestHash,
                    response = response,
                };
            return response;
        }

        public static BackendTransportResponse Json<T>(int statusCode, T body)
            where T : class => new()
        {
            statusCode = statusCode,
            bodyJson = UnityBackendJsonSerializer.Instance.ToJson(body),
        };

        public static BackendTransportResponse Error(int statusCode, string code, string message,
            string requestId, bool retryable)
        {
            var error = new BackendErrorDetails
            {
                code = code,
                message = message,
                requestId = requestId,
                retryable = retryable,
            };
            return new BackendTransportResponse
            {
                statusCode = statusCode,
                error = error,
                bodyJson = UnityBackendJsonSerializer.Instance.ToJson(
                    new BackendErrorEnvelope { error = error }),
            };
        }

        private static string RouteKey(string method, string path) =>
            (method ?? string.Empty).Trim().ToUpperInvariant() + " " + (path ?? string.Empty);
    }
}
