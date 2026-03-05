-- Delete existing system template
DELETE FROM templates WHERE id < 0;

-- Seed System Templates
INSERT INTO templates (id, user_id, name, description)
VALUES (-1, 'system', 'Video Games', 'Default review template for video games.');

-- Seed System Template Fields
INSERT INTO template_fields (id, template_id, name, display_name, position)
VALUES 
    (-11, -1, 'Gameplay', 'Gameplay', 1),
    (-14, -1, 'Graphics', 'Graphics', 2),
    (-12, -1, 'Story', 'Story', 3),
    (-13, -1, 'Sound', 'Sound', 4);