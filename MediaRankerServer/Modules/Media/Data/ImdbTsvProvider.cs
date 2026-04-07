using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace MediaRankerServer.Modules.Media.Services;

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

public class ImdbTsvProvider
{
    private static readonly string[] ExpectedHeaders = [
        "tconst", "titleType", "primaryTitle", "originalTitle",
        "isAdult", "startYear", "endYear", "runtimeMinutes", "genres"
    ];

    private readonly HttpClient httpClient;
    private readonly ILogger<ImdbTsvProvider> logger;

    public ImdbTsvProvider(HttpClient httpClient, ILogger<ImdbTsvProvider> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    /// <summary>
    /// Downloads the IMDB dataset, parses the TSV, and calls the provided handler for each batch of rows.
    /// </summary>
    public async Task DoImport(
        string datasetUrl,
        int batchSize,
        Func<List<ImdbTsvRow>, CancellationToken, Task> batchHandler,
        CancellationToken ct = default)
    {
        logger.LogInformation("Starting IMDB import. Dataset: {DatasetUrl}, Batch size: {BatchSize}", datasetUrl, batchSize);

        var totalRows = 0;
        var totalBatches = 0;

        await using var dataStream = await DownloadAndDecompressAsync(datasetUrl, ct);

        var batch = new List<ImdbTsvRow>(batchSize);

        await foreach (var row in ParseStreamAsync(dataStream, ct))
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

    private async IAsyncEnumerable<ImdbTsvRow> ParseStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream);

        // Read and validate header
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine == null)
        {
            throw new InvalidDataException("IMDB TSV file is empty or missing header.");
        }

        var headers = headerLine.Split('\t');
        if (!headers.SequenceEqual(ExpectedHeaders))
        {
            throw new InvalidDataException(
                $"IMDB TSV header mismatch. Expected: {string.Join(",", ExpectedHeaders)}, Got: {string.Join(",", headers)}");
        }

        // Stream rows
        string? line;
        int lineNumber = 1;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;

            var columns = line.Split('\t');
            if (columns.Length != ExpectedHeaders.Length)
            {
                logger.LogWarning("Skipping malformed row at line {LineNumber}: expected {ExpectedColumns} columns, got {ActualColumns}",
                    lineNumber, ExpectedHeaders.Length, columns.Length);
                continue;
            }

            // Skip adult content
            if (columns[4] == "1")
            {
                continue;
            }

            var row = new ImdbTsvRow(
                Tconst: columns[0],
                TitleType: columns[1],
                PrimaryTitle: SanitizeTitle(columns[2]),
                OriginalTitle: SanitizeTitle(columns[3]),
                IsAdult: columns[4] == "1",
                StartYear: ParseNullableInt(columns[5]),
                EndYear: ParseNullableInt(columns[6]),
                RuntimeMinutes: ParseNullableInt(columns[7]),
                Genres: columns[8] == @"\N" ? null : columns[8]
            );

            yield return row;
        }
    }

    private string SanitizeTitle(string title)
    {
        return title.Replace("{", "").Replace("}", "");
    }

    private int? ParseNullableInt(string value)
    {
        if (value == @"\N" || string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (int.TryParse(value, out var result))
        {
            return result;
        }

        return null;
    }
}
