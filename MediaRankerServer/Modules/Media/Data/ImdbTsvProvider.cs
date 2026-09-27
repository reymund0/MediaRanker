using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Options;
using MediaRankerServer.Modules.Media.Jobs;

namespace MediaRankerServer.Modules.Media.Data;

public record ImdbTsvRow(
    string Tconst,
    string TitleType,
    string PrimaryTitle,
    string OriginalTitle,
    bool IsAdult,
    int? StartYear,
    int? EndYear,
    int? RuntimeMinutes,
    string? Genres,
    string RawLine
);

public record ImdbEpisodeTsvRow(
    string Tconst,
    string ParentTconst,
    int SeasonNumber,
    int EpisodeNumber,
    string RawLine
);

public record ImdbRatingTsvRow(
    string Tconst,
    decimal AverageRating,
    int NumVotes,
    string RawLine
);

/// <summary>Downloads and strictly validates one IMDb gzip TSV feed while retaining its open stream across callbacks.</summary>
public class ImdbTsvProvider(
    HttpClient httpClient,
    IOptions<ImdbImportOptions> options,
    ILogger<ImdbTsvProvider> logger)
{
    public delegate TRow? ParseRowDelegate<TRow>(string[] columns, long lineNumber, string rawLine);
    public delegate Task BatchHandlerDelegate<TRow>(List<TRow> batch, CancellationToken cancellationToken);

    private readonly ImdbImportOptions config = options.Value;

    public Task<ImdbFeedResult> RunBatchImportAsync<TRow>(
        string datasetUrl,
        IReadOnlyList<string> expectedHeaders,
        ParseRowDelegate<TRow> parseRow,
        BatchHandlerDelegate<TRow> batchHandler,
        CancellationToken ct = default) =>
        RunBatchImportAsync(datasetUrl, expectedHeaders, parseRow, batchHandler, new ImdbImportExecution(config), ct);

    public async Task<ImdbFeedResult> RunBatchImportAsync<TRow>(
        string datasetUrl,
        IReadOnlyList<string> expectedHeaders,
        ParseRowDelegate<TRow> parseRow,
        BatchHandlerDelegate<TRow> batchHandler,
        ImdbImportExecution execution,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetUrl);
        ArgumentNullException.ThrowIfNull(parseRow);
        ArgumentNullException.ThrowIfNull(batchHandler);
        config.ValidateFiniteProfile();

        var tempPath = Path.Combine(Path.GetTempPath(), $"mediaranker-imdb-{Guid.NewGuid():N}.gz");
        DownloadResult download;
        var failed = false;
        try
        {
            download = await DownloadAsync(datasetUrl, tempPath, execution, ct);
            await using var compressed = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
            ReadGzipHeader(compressed, ct);
            using var oneByteInput = new OneByteReadStream(compressed);
            using var deflate = new DeflateStream(oneByteInput, CompressionMode.Decompress, leaveOpen: true);
            using var counted = new CountingReadStream(deflate, config.MaxDecompressedBytesPerFeed);

            var result = await ParseStreamAsync(counted, expectedHeaders, parseRow, batchHandler, execution, ct);
            counted.Dispose();
            deflate.Dispose();
            ValidateGzipTrailer(oneByteInput, counted.Crc32, counted.BytesRead, ct);
            return result with
            {
                CompressedBytes = download.CompressedBytes,
                DecompressedBytes = counted.BytesRead
            };
        }
        catch (DecoderFallbackException)
        {
            failed = true;
            // Invalid encoding is a strict feed rejection, not a transient I/O failure.
            throw new InvalidDataException("IMDb feed contains invalid UTF-8.");
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception cleanupError)
            {
                logger.LogError("IMDb feed temporary-file cleanup failed. ErrorCategory: {ErrorCategory}.", cleanupError.GetType().Name);
                // Keep the original rejection/cancellation category if ingestion already failed.
                if (!failed) throw new IOException("IMDb feed temporary-file cleanup failed.");
            }
        }
    }

    private async Task<DownloadResult> DownloadAsync(
        string datasetUrl,
        string tempPath,
        ImdbImportExecution execution,
        CancellationToken ct)
    {
        execution.ReserveHttpAttempt();
        logger.LogInformation("Starting IMDb feed download.");

        using var response = await httpClient.GetAsync(datasetUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"IMDb feed returned HTTP {(int)response.StatusCode}.");

        var advertisedLength = response.Content.Headers.ContentLength;
        if (advertisedLength.HasValue && advertisedLength.Value > config.MaxCompressedBytesPerFeed)
            throw new ImdbBudgetExceededException("IMDb compressed feed allowance exhausted.");

        long compressedBytes = 0;
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(), ct)) != 0)
            {
                if (compressedBytes > config.MaxCompressedBytesPerFeed - read ||
                    compressedBytes > config.MaxTemporaryDiskBytes - read)
                    throw new ImdbBudgetExceededException("IMDb compressed or temporary-disk allowance exhausted.");

                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                compressedBytes += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (advertisedLength.HasValue && advertisedLength.Value != compressedBytes)
            throw new InvalidDataException("IMDb feed content length did not match the completed transfer.");

        await output.FlushAsync(ct);
        return new DownloadResult(compressedBytes);
    }

    private async Task<ImdbFeedResult> ParseStreamAsync<TRow>(
        Stream stream,
        IReadOnlyList<string> expectedHeaders,
        ParseRowDelegate<TRow> parseRow,
        BatchHandlerDelegate<TRow> batchHandler,
        ImdbImportExecution execution,
        CancellationToken ct)
    {
        // Accept the UTF-8 preamble without switching to a permissive UTF-16/32 decoder.
        using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: false, bufferSize: 64 * 1024, leaveOpen: true);

        var lineReader = new BoundedLineReader(reader, config.MaxLineCharacters);
        var headerLine = await lineReader.ReadLineAsync(ct)
            ?? throw new InvalidDataException("IMDb TSV feed is empty or missing its header.");
        var headers = headerLine.Split('\t', StringSplitOptions.None);
        if (!headers.SequenceEqual(expectedHeaders, StringComparer.Ordinal))
            throw new InvalidDataException("IMDb TSV feed header did not match the expected structure.");

        long rowsRead = 0;
        long parsedRows = 0;
        long filteredRows = 0;
        long batches = 0;
        var batch = new List<TRow>(Math.Min(config.BatchSize, ImdbImportOptions.DefaultBatchSize));
        var batchCharacters = 0;
        string? line;
        long lineNumber = 1;

        async Task FlushBatchAsync()
        {
            // A positive yield applies between successful database units. Delay only
            // when another full batch is ready, so the final callback adds no tail wait.
            if (batches > 0 && config.YieldBetweenUnitsMilliseconds > 0)
                await Task.Delay(config.YieldBetweenUnitsMilliseconds, ct);
            ct.ThrowIfCancellationRequested();
            await batchHandler(batch, ct);
            batches++;
            execution.Counters.AddBatchUnit();
            batch = new List<TRow>(Math.Min(config.BatchSize, ImdbImportOptions.DefaultBatchSize));
            batchCharacters = 0;
            ct.ThrowIfCancellationRequested();
        }

        while ((line = await lineReader.ReadLineAsync(ct)) is not null)
        {
            lineNumber++;
            if (rowsRead >= config.MaxRowsPerFeed)
                throw new ImdbBudgetExceededException("IMDb feed row allowance exhausted.");
            rowsRead++;
            execution.Counters.AddRowsRead(1);

            var columns = line.Split('\t', StringSplitOptions.None);
            if (columns.Length != expectedHeaders.Count)
                throw new InvalidDataException("IMDb TSV feed contained a row with the wrong number of columns.");

            var row = parseRow(columns, lineNumber, line);
            if (row is null)
            {
                filteredRows++;
                execution.Counters.AddFiltered();
                continue;
            }

            parsedRows++;
            execution.Counters.AddParsed();

            // Only accepted rows contribute to a batch. A filtered row must not
            // force a database unit boundary.
            if (batch.Count > 0 && batchCharacters > config.MaxBatchCharacters - line.Length)
                await FlushBatchAsync();

            batch.Add(row);
            batchCharacters += line.Length;
            if (batch.Count < config.BatchSize && batchCharacters < config.MaxBatchCharacters)
                continue;

            await FlushBatchAsync();
        }

        if (batch.Count > 0)
            await FlushBatchAsync();

        return new ImdbFeedResult(rowsRead, parsedRows, filteredRows, batches, 0, 0);
    }

    private static void ValidateGzipTrailer(OneByteReadStream input, uint actualCrc, long actualLength, CancellationToken ct)
    {
        Span<byte> trailer = stackalloc byte[8];
        ReadExactly(input, trailer, ct);
        var expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(trailer[..4]);
        var expectedSize = BinaryPrimitives.ReadUInt32LittleEndian(trailer[4..]);
        if (expectedCrc != actualCrc || expectedSize != unchecked((uint)actualLength))
            throw new InvalidDataException("IMDb gzip feed trailer did not match the decompressed content.");
        ct.ThrowIfCancellationRequested();
        if (input.ReadByte() >= 0)
            throw new InvalidDataException("IMDb gzip feed contained trailing or concatenated content.");
    }

    private static void ReadGzipHeader(Stream stream, CancellationToken ct)
    {
        var bytes = new List<byte>(64);
        var fixedHeader = ReadHeaderBytes(stream, 10, bytes, ct);
        if (fixedHeader[0] != 0x1f || fixedHeader[1] != 0x8b || fixedHeader[2] != 8 || (fixedHeader[3] & 0xe0) != 0)
            throw new InvalidDataException("IMDb feed did not contain a supported gzip header.");

        var flags = fixedHeader[3];
        if ((flags & 0x04) != 0)
        {
            var extraLength = ReadHeaderBytes(stream, 2, bytes, ct);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(extraLength);
            ReadHeaderBytes(stream, length, bytes, ct);
        }
        if ((flags & 0x08) != 0) SkipZeroTerminated(stream, bytes, ct);
        if ((flags & 0x10) != 0) SkipZeroTerminated(stream, bytes, ct);
        if ((flags & 0x02) != 0)
        {
            var expectedHeaderCrc = BinaryPrimitives.ReadUInt16LittleEndian(ReadHeaderBytes(stream, 2, null, ct));
            var headerCrc = new Crc32Accumulator();
            headerCrc.Append(bytes.ToArray());
            var actualHeaderCrc = (ushort)(headerCrc.Value & 0xffff);
            if (expectedHeaderCrc != actualHeaderCrc)
                throw new InvalidDataException("IMDb gzip header integrity validation failed.");
        }
    }

    private static byte[] ReadHeaderBytes(Stream stream, int length, List<byte>? capture, CancellationToken ct)
    {
        var bytes = new byte[length];
        ReadExactly(stream, bytes, ct);
        capture?.AddRange(bytes);
        return bytes;
    }

    private static void SkipZeroTerminated(Stream stream, List<byte> capture, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var value = stream.ReadByte();
            if (value < 0) throw new InvalidDataException("IMDb gzip header is incomplete.");
            capture.Add((byte)value);
            if (value == 0) return;
        }
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer, CancellationToken ct = default)
    {
        while (!buffer.IsEmpty)
        {
            ct.ThrowIfCancellationRequested();
            var read = stream.Read(buffer);
            if (read == 0) throw new InvalidDataException("IMDb gzip feed ended before its envelope completed.");
            buffer = buffer[read..];
        }
    }

    private static void ReadExactly(Stream stream, byte[] buffer, CancellationToken ct = default) => ReadExactly(stream, buffer.AsSpan(), ct);

    private sealed record DownloadResult(long CompressedBytes);

    private sealed class BoundedLineReader(StreamReader reader, int maximumCharacters)
    {
        private readonly char[] buffer = new char[16 * 1024];
        private int offset;
        private int count;

        public async ValueTask<string?> ReadLineAsync(CancellationToken ct)
        {
            var line = new StringBuilder(Math.Min(maximumCharacters, 256));
            var sawCharacters = false;
            while (true)
            {
                if (offset >= count)
                {
                    count = await reader.ReadAsync(buffer.AsMemory(), ct);
                    offset = 0;
                    if (count == 0) return sawCharacters ? TrimCarriageReturn(line) : null;
                }

                var remaining = buffer.AsSpan(offset, count - offset);
                var newline = remaining.IndexOf('\n');
                var take = newline >= 0 ? newline : remaining.Length;
                if ((long)line.Length + take > maximumCharacters)
                    throw new ImdbBudgetExceededException("IMDb TSV line character allowance exhausted.");
                if (take > 0)
                {
                    line.Append(remaining[..take]);
                    sawCharacters = true;
                }
                offset += take;
                if (newline < 0) continue;
                offset++;
                return TrimCarriageReturn(line);
            }
        }

        private static string TrimCarriageReturn(StringBuilder line)
        {
            if (line.Length > 0 && line[^1] == '\r') line.Length--;
            return line.ToString();
        }
    }

    private sealed class OneByteReadStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty) return 0;
            var value = inner.ReadByte();
            if (value < 0) return 0;
            buffer[0] = (byte)value;
            return 1;
        }
        public override int ReadByte() => inner.ReadByte();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (buffer.IsEmpty) return ValueTask.FromResult(0);
            return inner.ReadAsync(buffer[..1], ct);
        }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class CountingReadStream(Stream inner, long maximum) : Stream
    {
        private readonly Crc32Accumulator crc = new();
        public long BytesRead { get; private set; }
        public uint Crc32 => crc.Value;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => BytesRead;
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            if (BytesRead >= maximum) return inner.ReadByte() < 0 ? 0 : throw new ImdbBudgetExceededException("IMDb decompressed-byte allowance exhausted.");
            var read = inner.Read(buffer[..(int)Math.Min(buffer.Length, maximum - BytesRead)]);
            if (read > 0) { BytesRead += read; crc.Append(buffer[..read]); }
            return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (BytesRead >= maximum)
            {
                var extra = new byte[1];
                var extraCount = await inner.ReadAsync(extra.AsMemory(), ct);
                if (extraCount != 0) throw new ImdbBudgetExceededException("IMDb decompressed-byte allowance exhausted.");
                return 0;
            }
            var allowed = buffer[..(int)Math.Min(buffer.Length, maximum - BytesRead)];
            var read = await inner.ReadAsync(allowed, ct);
            if (read > 0) { BytesRead += read; crc.Append(allowed[..read].Span); }
            return read;
        }
        public override int ReadByte()
        {
            Span<byte> one = stackalloc byte[1];
            return Read(one) == 0 ? -1 : one[0];
        }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class Crc32Accumulator
    {
        private static readonly uint[] Table = CreateTable();
        private uint crc = 0xffffffff;
        public uint Value => ~crc;
        public void Append(ReadOnlySpan<byte> bytes)
        {
            foreach (var value in bytes) crc = (crc >> 8) ^ Table[(int)((crc ^ value) & 0xff)];
        }
        private static uint[] CreateTable()
        {
            var table = new uint[256];
            for (var i = 0; i < table.Length; i++)
            {
                var value = (uint)i;
                for (var bit = 0; bit < 8; bit++) value = (value & 1) == 0 ? value >> 1 : 0xedb88320 ^ (value >> 1);
                table[i] = value;
            }
            return table;
        }
    }
}
