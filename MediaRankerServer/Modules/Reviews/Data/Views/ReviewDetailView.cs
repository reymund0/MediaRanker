using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaRankerServer.Modules.Reviews.Data.Views;

public class ReviewDetailView
{
    public long Id { get; set; }
    public string UserId { get; set; } = null!;
    public short OverallScore { get; set; }
    public string? ReviewTitle { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    
    public long MediaId { get; set; }
    public string MediaTitle { get; set; } = null!;
    public string? MediaCoverFileKey { get; set; }
    public long MediaTypeId { get; set; }
    public string MediaTypeName { get; set; } = null!;
    
    public long TemplateId { get; set; }
    public string TemplateName { get; set; } = null!;

    public class Configuration : IEntityTypeConfiguration<ReviewDetailView>
    {
        public void Configure(EntityTypeBuilder<ReviewDetailView> builder)
        {
            builder.HasNoKey();
            builder.ToView("review_details");
        }
    }
}
