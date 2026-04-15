using System.IO.Compression;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using MediaRankerServer.Modules.Media.Jobs;

namespace MediaRankerServer.Modules.Media.Data;

// Row records for TSV parsing
public record ImdbTsvRow(
    string Tconst,
    string TitleType,
    string PrimaryTitle,
    string OriginalTitle,
    bool IsAdult,
    int? StartYear,
    int? EndYear,
    int? RuntimeMinutes,
    string? Genres
);

public record ImdbEpisodeTsvRow(
    string Tconst,
    string ParentTconst,
    int SeasonNumber,
    int EpisodeNumber
);

public class ImdbTsvProvider(
    HttpClient httpClient,
    IOptions<ImdbImportOptions> options,
    ILogger<ImdbTsvProvider> logger)
{
    private readonly ImdbImportOptions config = options.Value;

    /// <summary>
    /// Downloads an IMDB TSV dataset, parses it, and calls the provided handler for each batch of rows.
    /// The datasets are in the tens of millions of rows, so we process them in batches to avoid memory issues.
    /// </summary>
    /// <typeparam name="TRow">The row type to parse into.</typeparam>
    /// <param name="datasetUrl">URL to download the gzipped TSV from.</param>
    /// <param name="expectedHeaders">Expected column headers in order. Throws if mismatch.</param>
    /// <param name="parseRow">Callback when a row is parsed. Return null to indicate skip row from batch.</param>
    /// <param name="batchHandler">Callback for the current batch of rows when batch limit is reached.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task RunBatchImportAsync<TRow>(
        string datasetUrl,
        IReadOnlyList<string> expectedHeaders,
        Func<string[], int, TRow?> parseRow,
        Func<List<TRow>, CancellationToken, Task> batchHandler,
        CancellationToken ct = default)
    {
        var batchSize = config.BatchSize;
        var totalRows = 0;
        var totalBatches = 0;

        logger.LogInformation("Starting IMDB import. Dataset: {DatasetUrl}, Batch size: {BatchSize}", datasetUrl, batchSize);

        await using var dataStream = await DownloadAndDecompressAsync(datasetUrl, ct);

        var batch = new List<TRow>(batchSize);

        await foreach (var row in ParseStreamAsync(dataStream, expectedHeaders, parseRow, ct))
        {
            batch.Add(row);
            totalRows++;

            if (batch.Count >= batchSize)
            {
                await batchHandler(batch, ct);
                totalBatches++;
                batch.Clear();
            }
        }

        // Handle remaining rows
        if (batch.Count > 0)
        {
            await batchHandler(batch, ct);
            totalBatches++;
        }

        logger.LogInformation("IMDB import completed. Total rows: {TotalRows}, Total batches: {TotalBatches}", totalRows, totalBatches);
    }

    private async Task<Stream> DownloadAndDecompressAsync(string url, CancellationToken ct)
    {
        logger.LogInformation("Downloading IMDB dataset from {Url}", url);

        var tempFile = Path.GetTempFileName();
        try
        {
            using (var downloadStream = await httpClient.GetStreamAsync(url, ct))
            using (var fileStream = File.OpenWrite(tempFile))
            {
                await downloadStream.CopyToAsync(fileStream, ct);
            }

            logger.LogInformation("Downloaded IMDB dataset to temp file: {TempFile}", tempFile);

            // Open with DeleteOnClose - temp file deletes when stream is disposed
            var fileStreamForDecompression = new FileStream(tempFile, FileMode.Open, FileAccess.Read,
                FileShare.None, bufferSize: 4096, FileOptions.DeleteOnClose);

            return new GZipStream(fileStreamForDecompression, CompressionMode.Decompress, leaveOpen: false);
        }
        catch
        {
            try { File.Delete(tempFile); } catch { }
            throw;
        }
    }

    private async IAsyncEnumerable<TRow> ParseStreamAsync<TRow>(
        Stream stream,
        IReadOnlyList<string> expectedHeaders,
        Func<string[], int, TRow?> parseRow,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream);

        // Read and validate header
        var headerLine = await reader.ReadLineAsync(ct)
            ?? throw new InvalidDataException("IMDB TSV file is empty or missing header.");

        var headers = headerLine.Split('\t');
        if (!headers.SequenceEqual(expectedHeaders))
        {
            throw new InvalidDataException(
                $"IMDB TSV header mismatch. Expected: {string.Join(",", expectedHeaders)}, Got: {string.Join(",", headers)}");
        }

        // Stream rows
        string? line;
        int lineNumber = 1;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;

            var columns = line.Split('\t');
            if (columns.Length != expectedHeaders.Count)
            {
                logger.LogWarning("Skipping malformed row at line {LineNumber}: expected {ExpectedColumns} columns, got {ActualColumns}",
                    lineNumber, expectedHeaders.Count, columns.Length);
                continue;
            }

            var row = parseRow(columns, lineNumber);
            if (row is not null)
            {
                yield return row;
            }
        }
    }
}
