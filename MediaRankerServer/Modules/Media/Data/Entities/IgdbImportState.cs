using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaRankerServer.Modules.Media.Data.Entities;

/// <summary>Single durable cursor for the resumable IGDB catalog importer.</summary>
public sealed class IgdbImportState
{
    public const long SingletonId = 1;

    public long Id { get; set; } = SingletonId;
    public bool BootstrapCompleted { get; set; }
    public bool RunIsBootstrap { get; set; }
    public long? RunMaximumId { get; set; }
    public long LastCommittedId { get; set; }
    public DateTimeOffset? RunUpdatedAfter { get; set; }
    public DateTimeOffset? RunUpdatedBefore { get; set; }
    public DateTimeOffset? BootstrapStartedAt { get; set; }
    public DateTimeOffset? LastCompletedUpdatedAt { get; set; }
    public Guid? ClaimToken { get; set; }
    public DateTimeOffset? ClaimedUntil { get; set; }
    public long Version { get; set; }

    public sealed class Configuration : IEntityTypeConfiguration<IgdbImportState>
    {
        public void Configure(EntityTypeBuilder<IgdbImportState> builder)
        {
            builder.ToTable("igdb_import_state", table => table.HasCheckConstraint("ck_igdb_import_state_singleton", "id = 1"));
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Version).IsConcurrencyToken();
        }
    }
}
