namespace UniGrader.Shared.Models;

public class GradeResults
{
    public Dictionary<string, string> Wrong { get; set; } = new();
    
    public double Grade => Points / TotalPoints;
    public double Points { get; set; }
    public double TotalPoints { get; set; }
}