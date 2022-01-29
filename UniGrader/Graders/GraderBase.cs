using UniGrader.Models;
using UniGrader.Shared.Models;

namespace UniGrader.Graders;

public abstract class GraderBase
{
    public Dictionary<string, string> Errors { get; } = new();

    public bool Success => Errors.Count <= 0;
    
    protected Language Language { get; }
    protected string RepoPath { get; }
    
    public GraderBase(Language lang, string repoBase)
    {
        Language = lang;
        RepoPath = repoBase;
    }
    
    public async Task<GradeResults?> Run(string data)
    {
        if (!await HasPrerequisites())
            return null;

        return await Grade(data);
    }

    /// <summary>
    /// Execute the grading process
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    protected abstract Task<GradeResults> Grade(string data);
    
    /// <summary>
    /// Do we have everything we need to execute?
    /// </summary>
    /// <returns>True if met, otherwise false</returns>
    protected abstract Task<bool> HasPrerequisites();
}