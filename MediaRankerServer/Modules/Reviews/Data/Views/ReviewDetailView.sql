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
    m.id AS media_id,
    m.title AS media_title,
    mc.file_key AS media_cover_file_key,
    m.media_type,
    r.template_id,
    t.name AS template_name
FROM reviews r
INNER JOIN media m ON m.id = r.media_id
INNER JOIN templates t ON t.id = r.template_id
LEFT JOIN media_covers mc ON mc.id = m.cover_id;
