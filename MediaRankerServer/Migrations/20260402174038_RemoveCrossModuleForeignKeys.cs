using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaRankerServer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop cross-module FK: reviews.media_id → media.id (if it exists)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.table_constraints 
                        WHERE constraint_name = 'fk_reviews_media_media_id'
                    ) THEN
                        ALTER TABLE reviews DROP CONSTRAINT fk_reviews_media_media_id;
                    END IF;
                END $$;");

            // Drop cross-module FK: reviews.template_id → templates.id (if it exists)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.table_constraints 
                        WHERE constraint_name = 'fk_reviews_templates_template_id'
                    ) THEN
                        ALTER TABLE reviews DROP CONSTRAINT fk_reviews_templates_template_id;
                    END IF;
                END $$;");

            // Drop cross-module FK: review_fields.template_field_id → template_fields.id (if it exists)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.table_constraints 
                        WHERE constraint_name = 'fk_review_fields_template_fields_template_field_id'
                    ) THEN
                        ALTER TABLE review_fields DROP CONSTRAINT fk_review_fields_template_fields_template_field_id;
                    END IF;
                END $$;");

            // Drop cross-module FK: templates.media_type_id → media_types.id (if it exists)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.table_constraints 
                        WHERE constraint_name = 'fk_templates_media_types_media_type_id'
                    ) THEN
                        ALTER TABLE templates DROP CONSTRAINT fk_templates_media_types_media_type_id;
                    END IF;
                END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add cross-module FK: reviews.media_id → media.id (if it doesn't exist)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints 
                        WHERE constraint_name = 'fk_reviews_media_media_id'
                    ) THEN
                        ALTER TABLE reviews ADD CONSTRAINT fk_reviews_media_media_id
                        FOREIGN KEY (media_id) REFERENCES media(id) ON DELETE CASCADE;
                    END IF;
                END $$;");

            // Add cross-module FK: reviews.template_id → templates.id (if it doesn't exist)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints 
                        WHERE constraint_name = 'fk_reviews_templates_template_id'
                    ) THEN
                        ALTER TABLE reviews ADD CONSTRAINT fk_reviews_templates_template_id
                        FOREIGN KEY (template_id) REFERENCES templates(id) ON DELETE CASCADE;
                    END IF;
                END $$;");

            // Add cross-module FK: review_fields.template_field_id → template_fields.id (if it doesn't exist)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints 
                        WHERE constraint_name = 'fk_review_fields_template_fields_template_field_id'
                    ) THEN
                        ALTER TABLE review_fields ADD CONSTRAINT fk_review_fields_template_fields_template_field_id
                        FOREIGN KEY (template_field_id) REFERENCES template_fields(id) ON DELETE CASCADE;
                    END IF;
                END $$;");

            // Add cross-module FK: templates.media_type_id → media_types.id (if it doesn't exist)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints 
                        WHERE constraint_name = 'fk_templates_media_types_media_type_id'
                    ) THEN
                        ALTER TABLE templates ADD CONSTRAINT fk_templates_media_types_media_type_id
                        FOREIGN KEY (media_type_id) REFERENCES media_types(id) ON DELETE CASCADE;
                    END IF;
                END $$;");
        }
    }
}
