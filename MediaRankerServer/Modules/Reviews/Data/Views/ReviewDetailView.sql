CREATE VIEW review_details AS
SELECT
    r.id,
    r.user_id,
    r.overall_score,
    r.review_title,
    r.notes,
    r.consumed_at,
    r.created_at,
    r.updated_at,
    r.media_id,
    r.media_collection_id,
    r.template_id,
    CASE WHEN r.media_collection_id IS NOT NULL THEN 'Series'
         WHEN m.media_type = 'TvShow' AND collection.collection_type = 'Season' THEN 'Episode'
         ELSE 'Title' END AS review_kind,
    COALESCE(reviewed_series.title, m.title) AS media_title,
    CASE WHEN r.media_collection_id IS NOT NULL THEN reviewed_series.cover_id
         WHEN m.media_type = 'TvShow' AND collection.collection_type = 'Season' THEN series.cover_id
         WHEN m.external_source = 'Imdb' AND m.media_type = 'TvShow'
             THEN CASE WHEN collection.collection_type = 'Series' THEN collection.cover_id ELSE series.cover_id END
         ELSE m.cover_id END AS media_cover_id,
    COALESCE(reviewed_series.media_type, m.media_type) AS media_type,
    CASE WHEN m.media_type = 'TvShow' AND collection.collection_type = 'Series' THEN collection.id ELSE COALESCE(reviewed_series.id, series.id) END AS series_id,
    COALESCE(reviewed_series.title, series.title, CASE WHEN m.media_type = 'TvShow' AND collection.collection_type = 'Series' THEN collection.title END) AS series_title,
    collection.season_number,
    m.episode_number,
    EXTRACT(YEAR FROM COALESCE(reviewed_series.release_date, series.release_date))::int AS series_start_year,
    EXTRACT(YEAR FROM COALESCE(latest_season.release_date, reviewed_series.release_date, series.release_date))::int AS series_end_year,
    hierarchy.season_count,
    hierarchy.episode_count,
    t.name AS template_name
FROM reviews r
LEFT JOIN media m ON r.media_id = m.id
LEFT JOIN media_collections collection ON m.media_collection_id = collection.id
LEFT JOIN media_collections series ON series.media_type = 'TvShow' AND series.collection_type = 'Series'
    AND (collection.parent_media_collection_id = series.id OR collection.id = series.id)
LEFT JOIN media_collections reviewed_series ON r.media_collection_id = reviewed_series.id
LEFT JOIN LATERAL (
    SELECT
        COUNT(*)::int AS season_count,
        COALESCE(SUM(episode_counts.episode_count), 0)::int AS episode_count
    FROM media_collections numbered_seasons
    LEFT JOIN LATERAL (
        SELECT COUNT(*)::int AS episode_count
        FROM media episodes
        WHERE episodes.media_collection_id = numbered_seasons.id
          AND episodes.media_type = 'TvShow'
    ) episode_counts ON TRUE
    WHERE numbered_seasons.parent_media_collection_id = COALESCE(
        reviewed_series.id,
        series.id,
        CASE WHEN collection.collection_type = 'Series' THEN collection.id END)
      AND numbered_seasons.collection_type = 'Season'
      AND numbered_seasons.season_number IS NOT NULL
      AND numbered_seasons.media_type = 'TvShow'
) hierarchy ON TRUE
LEFT JOIN LATERAL (
    SELECT numbered_seasons.release_date
    FROM media_collections numbered_seasons
    WHERE numbered_seasons.parent_media_collection_id = COALESCE(
        reviewed_series.id,
        series.id,
        CASE WHEN collection.collection_type = 'Series' THEN collection.id END)
      AND numbered_seasons.collection_type = 'Season'
      AND numbered_seasons.season_number IS NOT NULL
      AND numbered_seasons.media_type = 'TvShow'
    ORDER BY numbered_seasons.season_number DESC
    LIMIT 1
) latest_season ON TRUE
INNER JOIN templates t ON r.template_id = t.id;
