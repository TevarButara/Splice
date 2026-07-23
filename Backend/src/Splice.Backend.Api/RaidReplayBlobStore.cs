using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Splice.Backend.Api;

public sealed record RaidReplayBlobArtifact(
    string Provider, string Key, string ETag, long ContentLength, string Encoding);

public sealed class RaidReplayBlobException(
    string code, string message, bool retryable = true, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
}

public interface IRaidReplayBlobStore
{
    Task<RaidReplayBlobArtifact> PutAsync(
        Guid raidId, Guid resultId, string commandStreamHash,
        ReadOnlyMemory<byte> uncompressedJson, CancellationToken cancellationToken);

    Task<byte[]> GetAsync(
        string provider, string key, string etag, long contentLength,
        string encoding, CancellationToken cancellationToken);
}

// Local-only adapter for development and deterministic integration tests. Production can bind the
// same contract to S3-compatible storage without changing raid metadata or the Unity API response.
public sealed class LocalFileRaidReplayBlobStore : IRaidReplayBlobStore
{
    public const string ProviderName = "local-filesystem";
    public const int MaximumBlobBytes = 16 * 1024 * 1024;
    public const int MaximumUncompressedBytes = 32 * 1024 * 1024;

    private readonly string root;

    public LocalFileRaidReplayBlobStore(IConfiguration configuration)
    {
        var configured = configuration["ReplayStorage:LocalRoot"];
        root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "splice-replay-blobs")
            : configured);
        Directory.CreateDirectory(root);
    }

    public async Task<RaidReplayBlobArtifact> PutAsync(
        Guid raidId, Guid resultId, string commandStreamHash,
        ReadOnlyMemory<byte> uncompressedJson, CancellationToken cancellationToken)
    {
        if (uncompressedJson.IsEmpty || uncompressedJson.Length > MaximumUncompressedBytes)
            throw new RaidReplayBlobException(
                "REPLAY_BLOB_TOO_LARGE", "Replay command stream exceeds the storage limit.", false);

        var key = BuildKey(raidId, resultId, commandStreamHash);
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var compressed = Compress(uncompressedJson.Span);
        if (compressed.Length > MaximumBlobBytes)
            throw new RaidReplayBlobException(
                "REPLAY_BLOB_TOO_LARGE", "Compressed replay exceeds the storage limit.", false);
        var etag = Sha256(compressed);

        if (File.Exists(path))
        {
            await VerifyExistingAsync(path, etag, compressed.Length, cancellationToken);
            return new RaidReplayBlobArtifact(ProviderName, key, etag, compressed.Length, "gzip");
        }

        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew,
                             FileAccess.Write, FileShare.None, 65536,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(compressed, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            try
            {
                File.Move(temporaryPath, path, false);
            }
            catch (IOException) when (File.Exists(path))
            {
                File.Delete(temporaryPath);
                await VerifyExistingAsync(path, etag, compressed.Length, cancellationToken);
            }
        }
        catch (RaidReplayBlobException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            TryDeleteTemporary(temporaryPath);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDeleteTemporary(temporaryPath);
            throw new RaidReplayBlobException(
                "REPLAY_BLOB_WRITE_FAILED", "Replay object storage could not persist the blob.",
                true, exception);
        }

        return new RaidReplayBlobArtifact(ProviderName, key, etag, compressed.Length, "gzip");
    }

    public async Task<byte[]> GetAsync(
        string provider, string key, string etag, long contentLength,
        string encoding, CancellationToken cancellationToken)
    {
        if (provider != ProviderName || encoding != "gzip" ||
            contentLength is < 1 or > MaximumBlobBytes ||
            etag.Length != 64)
            throw new RaidReplayBlobException(
                "REPLAY_BLOB_METADATA_INVALID", "Replay storage metadata is invalid.", false);

        var path = ResolvePath(key);
        if (!File.Exists(path))
            throw new RaidReplayBlobException(
                "REPLAY_BLOB_MISSING", "Replay blob is not currently available.");
        byte[] compressed;
        try
        {
            var info = new FileInfo(path);
            if (info.Length != contentLength)
                throw new RaidReplayBlobException(
                    "REPLAY_BLOB_CORRUPT", "Replay blob length does not match immutable metadata.");
            compressed = await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (RaidReplayBlobException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new RaidReplayBlobException(
                "REPLAY_BLOB_READ_FAILED", "Replay object storage could not read the blob.",
                true, exception);
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(Sha256(compressed)),
                Encoding.ASCII.GetBytes(etag)))
            throw new RaidReplayBlobException(
                "REPLAY_BLOB_CORRUPT", "Replay blob hash does not match immutable metadata.");
        return DecompressBounded(compressed);
    }

    public string ResolvePathForDiagnostics(string key) => ResolvePath(key);

    private string ResolvePath(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 512 ||
            Path.IsPathRooted(key) || key.Contains('\\'))
            throw new RaidReplayBlobException(
                "REPLAY_BLOB_METADATA_INVALID", "Replay object key is invalid.", false);
        var segments = key.Split('/');
        if (segments.Any(segment => segment is "" or "." or ".."))
            throw new RaidReplayBlobException(
                "REPLAY_BLOB_METADATA_INVALID", "Replay object key is invalid.", false);
        var path = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.Ordinal))
            throw new RaidReplayBlobException(
                "REPLAY_BLOB_METADATA_INVALID", "Replay object key escapes its storage root.", false);
        return path;
    }

    private static string BuildKey(Guid raidId, Guid resultId, string commandStreamHash)
    {
        if (commandStreamHash.Length != 64 ||
            commandStreamHash.Any(character =>
                !char.IsAsciiHexDigit(character) || char.IsUpper(character)))
            throw new RaidReplayBlobException(
                "REPLAY_BLOB_METADATA_INVALID", "Command stream hash is invalid.", false);
        var raid = raidId.ToString("N");
        return $"replays/v1/{raid[..2]}/{raidId:D}/{resultId:D}-{commandStreamHash}.json.gz";
    }

    private static byte[] Compress(ReadOnlySpan<byte> source)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, true))
            gzip.Write(source);
        return output.ToArray();
    }

    private static byte[] DecompressBounded(byte[] compressed)
    {
        try
        {
            using var input = new MemoryStream(compressed, false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[65536];
            while (true)
            {
                var read = gzip.Read(buffer);
                if (read == 0) break;
                if (output.Length + read > MaximumUncompressedBytes)
                    throw new RaidReplayBlobException(
                        "REPLAY_BLOB_TOO_LARGE", "Replay blob expands beyond the storage limit.", false);
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }
        catch (RaidReplayBlobException)
        {
            throw;
        }
        catch (InvalidDataException exception)
        {
            throw new RaidReplayBlobException(
                "REPLAY_BLOB_CORRUPT", "Replay blob compression is invalid.", false, exception);
        }
    }

    private static async Task VerifyExistingAsync(
        string path, string expectedEtag, int expectedLength,
        CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length != expectedLength)
                throw new RaidReplayBlobException(
                    "REPLAY_BLOB_CONFLICT",
                    "Immutable replay key already contains different bytes.", false);
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(Sha256(bytes)),
                    Encoding.ASCII.GetBytes(expectedEtag)))
                throw new RaidReplayBlobException(
                    "REPLAY_BLOB_CONFLICT",
                    "Immutable replay key already contains different bytes.", false);
        }
        catch (RaidReplayBlobException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new RaidReplayBlobException(
                "REPLAY_BLOB_WRITE_FAILED",
                "Replay object storage could not verify an existing immutable blob.",
                true, exception);
        }
    }

    private static string Sha256(ReadOnlySpan<byte> value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));

    private static void TryDeleteTemporary(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // A uniquely named temp file can be reclaimed by the storage maintenance job.
        }
    }
}
