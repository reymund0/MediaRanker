using FluentValidation;
using MediatR;
using MediaRankerServer.Modules.Media.Contracts;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Media.Events;
using MediaRankerServer.Modules.Media.Services.Interfaces;
using MediaRankerServer.Shared.Data;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.Shared.Paging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MediaRankerServer.Modules.Media.Services;

public class MediaService(
    PostgreSQLContext dbContext,
    IArtworkService artworkService,
    IValidator<MediaUpsertRequest> mediaUpsertRequestValidator,
    IPublisher publisher,
    IMemoryCache? memoryCache = null,
    TimeProvider? timeProvider = null
) : IMediaService
{
    private const long ShowcaseHashModulus = 2_147_483_647L;
    private static readonly SemaphoreSlim ShowcaseCacheGate = new(1, 1);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<PageResult<MediaDto>> GetAllMediaAsync(string? mediaType, PageRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(mediaType))
            throw new DomainException("Media type is required.", "media_validation_error");

        if (!MediaTypes.IsValid(mediaType))
            throw new DomainException("Media type not found.", "media_type_not_found");

        var v = PagingValidator.Validate(request, MediaQueryBuilder.SortFields, MediaQueryBuilder.SearchFields, "title");

        var query = MediaQueryBuilder.ApplySearch(
            MediaQueryBuilder.BaseQuery(dbContext).Where(m => m.MediaType == mediaType), v);
        int? totalCount = null;
        if (request.IncludeTotalCount == true) 
            totalCount = await query.CountAsync(cancellationToken);
            
        query = MediaQueryBuilder.ApplySort(query, v);

        var page = await query.Skip(v.Skip).Take(v.Take).ToListAsync(cancellationToken);
        var covers = await artworkService.GetMediaArtworkAsync(page.Select(m => m.Id), cancellationToken);

        return new PageResult<MediaDto>(
            [.. page.Select(m => MediaDtoMapper.Map(m, covers?.GetValueOrDefault(m.Id)))],
            totalCount, v.Page, v.PageSize);
    }

    public async Task<MediaDto?> GetMediaByIdAsync(long mediaId, CancellationToken cancellationToken, bool requestArtwork = true)
    {
        var media = await MediaQueryBuilder.BaseQuery(dbContext)
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);

        if (media is null) return null;
        if (!requestArtwork) return MediaDtoMapper.Map(media);
        var covers = await artworkService.GetMediaArtworkAsync([media.Id], cancellationToken);
        return MediaDtoMapper.Map(media, covers?.GetValueOrDefault(media.Id));
    }

    public async Task<List<ShowcaseMediaDto>> GetShowcaseAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var utcDate = DateOnly.FromDateTime(now.UtcDateTime);
        var nextUtcMidnight = new DateTimeOffset(utcDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var cacheKey = $"media-showcase:{utcDate:yyyy-MM-dd}";
        if (memoryCache?.TryGetValue(cacheKey, out List<ShowcaseMediaDto>? cached) == true && cached is not null)
            return cached;

        // Service instances are scoped; share creation so every caller gets the first daily result.
        await ShowcaseCacheGate.WaitAsync(cancellationToken);
        try
        {
            // A waiter may cross midnight while another caller creates the previous day's entry.
            now = _timeProvider.GetUtcNow();
            utcDate = DateOnly.FromDateTime(now.UtcDateTime);
            nextUtcMidnight = new DateTimeOffset(utcDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            cacheKey = $"media-showcase:{utcDate:yyyy-MM-dd}";
            if (memoryCache?.TryGetValue(cacheKey, out cached) == true && cached is not null)
                return cached;

            // A date-seeded affine permutation gives each UTC day a stable, bounded SQL ordering.
            var dayNumber = (long)utcDate.DayNumber;
            var multiplier = (dayNumber * 104_729L % (ShowcaseHashModulus - 1)) + 1;
            var offset = dayNumber * 130_363L % ShowcaseHashModulus;
            var candidates = await dbContext.Media
                .AsNoTracking()
                .Where(m => m.Cover != null
                    && m.Cover.Outcome == CoverOutcome.Ready
                    && m.Cover.ExpiresAt >= nextUtcMidnight)
                .OrderBy(m => ((m.Id % ShowcaseHashModulus) * multiplier + offset) % ShowcaseHashModulus)
                .ThenBy(m => m.Id)
                .Take(20)
                .Select(m => new { m.Title, Cover = m.Cover! })
                .ToListAsync(cancellationToken);

            var result = candidates
                .Select(m => new { m.Title, Cover = ArtworkPresentation.Map(m.Cover, now) })
                .Where(m => m.Cover.Status == "ready" && m.Cover.Url is not null)
                .Select(m => new ShowcaseMediaDto { Title = m.Title, CoverImageUrl = m.Cover.Url! })
                .ToList();

            if (memoryCache is not null)
            {
                memoryCache.Set(cacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpiration = nextUtcMidnight });
            }

            return result;
        }
        finally
        {
            ShowcaseCacheGate.Release();
        }
    }

    public async Task<MediaDto> CreateMediaAsync(string userId, MediaUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateMediaRequestOrThrow(request, cancellationToken);

        var normalizedTitle = request.Title.Trim();
        var duplicateExists = await dbContext.Media.AnyAsync(
            m => m.Title == normalizedTitle
                && m.MediaType == request.MediaType
                && m.ReleaseDate == request.ReleaseDate,
            cancellationToken
        );

        if (duplicateExists)
        {
            throw new DomainException("Media already exists for the selected title, media type, and release date.", "media_conflict");
        }

        var media = new MediaEntity
        {
            Title = normalizedTitle,
            MediaType = request.MediaType,
            ReleaseDate = request.ReleaseDate
        };

        dbContext.Media.Add(media);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMediaByIdAsync(media.Id, cancellationToken)
            ?? throw new DomainException("Media was created but could not be loaded.", "media_load_failed");
    }

    public async Task<MediaDto> UpdateMediaAsync(string userId, long mediaId, MediaUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateMediaRequestOrThrow(request, cancellationToken);

        var media = await dbContext.Media
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken)
            ?? throw new DomainException("Media not found.", "media_not_found");

        var normalizedTitle = request.Title.Trim();
        var duplicateExists = await dbContext.Media.AnyAsync(
            m => m.Id != mediaId
                && m.Title == normalizedTitle
                && m.MediaType == request.MediaType
                && m.ReleaseDate == request.ReleaseDate,
            cancellationToken
        );

        if (duplicateExists)
        {
            throw new DomainException("Media already exists for the selected title, media type, and release date.", "media_conflict");
        }

        media.Title = normalizedTitle;
        media.MediaType = request.MediaType;
        media.ReleaseDate = request.ReleaseDate;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMediaByIdAsync(media.Id, cancellationToken)
            ?? throw new DomainException("Media was updated but could not be loaded.", "media_load_failed");
    }

    public async Task DeleteMediaAsync(long mediaId, CancellationToken cancellationToken = default)
    {
        var media = await dbContext.Media
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken)
            ?? throw new DomainException("Media not found.", "media_not_found");

        dbContext.Media.Remove(media);
        await dbContext.SaveChangesAsync(cancellationToken);

        await publisher.Publish(new MediaDeletedEvent(mediaId), cancellationToken);
    }

    private async Task ValidateMediaRequestOrThrow(MediaUpsertRequest request, CancellationToken cancellationToken)
    {
        var validationResult = mediaUpsertRequestValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            throw new DomainException(validationResult.Errors[0].ErrorMessage, "media_validation_error");
        }

        if (!MediaTypes.IsValid(request.MediaType))
        {
            throw new DomainException("Media type not found.", "media_type_not_found");
        }

    }
}
