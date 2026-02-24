namespace MediaRankerServer.Data.Seeds;

// We use negative numbers for seed ids to avoid collision with table generated ids.

public static class SeedIds
{
    // System User
    public static readonly string SystemUserId = "system";

    // ================================
    // Templates
    // ================================
    public static readonly long VideoGameBasicTemplateId = -1;

    // ================================
    // Template Fields
    // ================================

    // Video Game (-1)
    public static readonly long VideoGameGameplayFieldId = -11;
    public static readonly long VideoGameStoryFieldId = -12;
    public static readonly long VideoGameSoundFieldId = -13;
    public static readonly long VideoGameGraphicsFieldId = -14;

    // ================================
    // Template Fields END
    // ================================
}