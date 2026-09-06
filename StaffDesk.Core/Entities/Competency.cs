namespace StaffDesk.Core.Entities;

public class Competency
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    /// <summary>Minimum SeniorityLevel.Rank that may be rated on this competency (null = all).</summary>
    public int? MinSeniorityRank { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CompetencyLevelDescriptor> LevelDescriptors { get; set; } = new List<CompetencyLevelDescriptor>();
}

public class CompetencyLevelDescriptor
{
    public int Id { get; set; }
    public int CompetencyId { get; set; }
    public Competency Competency { get; set; } = null!;
    public int SeniorityLevelId { get; set; }
    public SeniorityLevel SeniorityLevel { get; set; } = null!;
    public string Descriptor { get; set; } = string.Empty;
}
