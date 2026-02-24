using System;
using MediaRankerServer.Data.Entities;
using MediaRankerServer.Data.Seeds;

public static class SystemTemplates
{
  public static IEnumerable<(Template template, TemplateField[] templateFields)> GenerateSeeds()
  {
    List<(Template template, TemplateField[] templateFields)> seeds = [];
    var videoGameTemplate = new Template
    {
      Id = VideoGameBasicTemplateId,
      UserId = SeedUtils.SystemUserId,
      Name = "Video Games",
      Description = "Default review template for video games."
    };

    var videoGameFields = new[]
    {
            new TemplateField
            {
                Id = VideoGameGameplayFieldId,
                TemplateId = VideoGameBasicTemplateId,
                Name = "gameplay",
                DisplayName = "Gameplay",
                Position = 1
            },
            new TemplateField
            {
                Id = VideoGameGraphicsFieldId,
                TemplateId = VideoGameBasicTemplateId,
                Name = "graphics",
                DisplayName = "Graphics",
                Position = 2
            },
            new TemplateField
            {
                Id = VideoGameStoryFieldId,
                TemplateId = VideoGameBasicTemplateId,
                Name = "story",
                DisplayName = "Story",
                Position = 3
            },
            new TemplateField
            {
                Id = VideoGameSoundFieldId,
                TemplateId = VideoGameBasicTemplateId,
                Name = "sound",
                DisplayName = "Sound",
                Position = 4
            }
        };

    seeds.Add((videoGameTemplate, videoGameFields));
    return seeds;
  }

  // Seed Ids
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
}