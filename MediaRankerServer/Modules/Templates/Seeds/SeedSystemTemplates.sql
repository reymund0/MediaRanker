-- Seed System Templates
INSERT INTO templates (id, media_type_id, user_id, name, description)
VALUES (-1, -1, 'system', 'Video Games', 'Default review template for video games.');

-- Seed System Template Fields
INSERT INTO template_fields (id, template_id, name, position)
VALUES 
    (-11, -1, 'Gameplay', 0),
    (-12, -1, 'Graphics', 1),
    (-13, -1, 'Story', 2),
    (-14, -1, 'Sound', 3);