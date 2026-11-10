using System.Globalization;
using System.Net;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.S3;

/// <summary>
/// <see cref="IUserKeysStore"/> and <see cref="IE2eProfileIndexStore"/> in the user's prefix of the data bucket, beside
/// their files: <c>&lt;identity id&gt;/keys/&lt;scope id&gt;</c> for each protected profile's keys document, and
/// <c>&lt;identity id&gt;/keys/index</c> for the index of them. Every write is conditional: <c>If-Match</c> with the
/// version read, or <c>If-None-Match: *</c> to create, so two devices cannot both change a document. The documents are
/// already wrapped under the profile's passphrase, so they are stored as they are. No sync profile may match
/// <c>keys/</c> (<see cref="SyncDefaults.KeysFolder"/>), so sync never sees them. The bucket keeps replaced and deleted
/// versions for 30 days, and the stack does not let users' credentials remove versions.
/// </summary>
/// <param name="targetProvider">Supplies the S3 client, bucket and the user's prefix.</param>
/// <param name="offlineModeService">Whether the user chose to work offline.</param>
/// <param name="timeProvider">Dates each write.</param>
public class S3UserKeysStore(IS3SyncTargetProvider targetProvider, IOfflineModeService offlineModeService, TimeProvider timeProvider)
    : IUserKeysStore, IE2eProfileIndexStore
{
    private const string ContentType = "application/json";

    private readonly IS3SyncTargetProvider targetProvider = targetProvider ?? throw new ArgumentNullException(nameof(targetProvider));
    private readonly IOfflineModeService offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public Task<StoredDocument?> GetAsync(E2eKeyScope scope, CancellationToken ct = default) =>
        GetDocumentAsync(KeysName(scope), ct);

    /// <inheritdoc/>
    public Task<string> SetAsync(E2eKeyScope scope, string value, string ifMatch, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ifMatch);

        return PutDocumentAsync(KeysName(scope), value, ifMatch, ct);
    }

    /// <inheritdoc/>
    public async Task<string?> TryCreateAsync(E2eKeyScope scope, string value, CancellationToken ct = default)
    {
        try
        {
            return await PutDocumentAsync(KeysName(scope), value, null, ct);
        }
        catch (E2eDocumentChangedException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        var name = KeysName(scope);
        return CallAsync(
            async target =>
            {
                try
                {
                    // Versioned bucket: this only adds a delete marker, so the keys stay restorable by an operator.
                    await target.Client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = target.Bucket, Key = target.RootPrefix + name }, ct);
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Nothing to delete.
                }

                return true;
            });
    }

    /// <inheritdoc/>
    public Task<StoredDocument?> GetIndexAsync(CancellationToken ct = default) =>
        GetDocumentAsync(IndexName(), ct);

    /// <inheritdoc/>
    public Task<string> SetIndexAsync(string value, string? ifMatch, CancellationToken ct = default) =>
        PutDocumentAsync(IndexName(), value, ifMatch, ct);

    private static string KeysName(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return $"{SyncDefaults.KeysFolder}/{scope.Id}";
    }

    private static string IndexName() => $"{SyncDefaults.KeysFolder}/{SyncDefaults.KeysIndexName}";

    private Task<StoredDocument?> GetDocumentAsync(string name, CancellationToken ct) =>
        CallAsync(async target =>
        {
            try
            {
                using var response = await target.Client.GetObjectAsync(target.Bucket, target.RootPrefix + name, ct);
                using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);
                return new StoredDocument(await reader.ReadToEndAsync(ct), response.ETag);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return (StoredDocument?)null;
            }
        });

    private Task<string> PutDocumentAsync(string name, string value, string? ifMatch, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(value);

        return CallAsync(async target =>
        {
            var request = new PutObjectRequest
            {
                BucketName = target.Bucket,
                Key = target.RootPrefix + name,
                ContentBody = value,
                ContentType = ContentType,

                // Browsers cannot send aws-chunked bodies.
                UseChunkEncoding = false,
            };
            request.Metadata[S3SyncRemoteStore.ModifiedAtMetadata] = timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
            if (ifMatch != null)
            {
                request.IfMatch = ifMatch;
            }
            else
            {
                request.IfNoneMatch = "*";
            }

            try
            {
                var response = await target.Client.PutObjectAsync(request, ct);
                return response.ETag;
            }
            catch (AmazonS3Exception ex) when (
                ex.StatusCode == HttpStatusCode.PreconditionFailed
                || ex.ErrorCode == "ConditionalRequestConflict"
                || (ifMatch != null && ex.StatusCode == HttpStatusCode.NotFound))
            {
                // Changed, created or deleted (If-Match on a missing object is a 404) since it was read.
                throw new E2eDocumentChangedException(ex);
            }
        });
    }

    private async Task<T> CallAsync<T>(Func<Models.S3SyncTarget, Task<T>> call)
    {
        if (offlineModeService.IsOffline)
        {
            // Not "cannot be used in this session" (InvalidOperationException): offline, the keys are just out of
            // reach, so this device's own copy is used.
            throw new HttpRequestException("Offline mode: the user's keys cannot be fetched.");
        }

        // HttpRequestException from here means the credentials service could not be reached, which is what it means
        // to callers too.
        var target = await targetProvider.GetTargetAsync()
            ?? throw new InvalidOperationException("The user's keys are only available when signed in with access to the data bucket.");

        try
        {
            return await call(target);
        }
        catch (AmazonServiceException ex) when (ex.InnerException is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new HttpRequestException(ex.Message, ex);
        }
    }
}
