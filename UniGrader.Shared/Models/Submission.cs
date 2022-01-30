using System.Text;
using System.Text.RegularExpressions;

namespace UniGrader.Shared.Models;

public class Submission
{
    private static Regex s_sanitizeName = new(@"[^\w]");
    
    /// <summary>
    /// Name of individual or team who submitted this
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Link to their public repository
    /// </summary>
    public string GithubUrl { get; set; }
    
    /// <summary>
    /// Docker-safe version of <see cref="Name"/>
    /// </summary>
    public string SanitizedName => s_sanitizeName.Replace(Name, "_");

    public override string ToString()
    {
        StringBuilder sb = new();
        string indent = new('\t', 2);
        sb.AppendLine();
        sb.AppendLine($"{indent}Name: {Name}");
        sb.AppendLine($"{indent}Sanitized Name: {SanitizedName}");
        sb.AppendLine($"{indent}Repo: {GithubUrl}");
        return sb.ToString();
    }
}