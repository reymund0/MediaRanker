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
    r.template_id,
    m.title AS media_title,
    CASE WHEN m.external_source = 'Imdb' AND m.media_type = 'TvShow'
        THEN CASE WHEN collection.collection_type = 'Series' THEN collection.cover_id ELSE series.cover_id END
        ELSE m.cover_id END AS media_cover_id,
    m.media_type,
    t.name AS template_name
FROM reviews r
INNER JOIN media m ON r.media_id = m.id
LEFT JOIN media_collections collection ON m.media_collection_id = collection.id
LEFT JOIN media_collections series ON collection.parent_media_collection_id = series.id AND series.collection_type = 'Series'
INNER JOIN templates t ON r.template_id = t.id;
