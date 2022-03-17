using UniGrader.Shared.Models;

namespace UniGrader.Platforms;

/// <summary>
/// Tailored towards executing your test-code against submission's code-base
/// </summary>
public class FunctionalPlatform : PlatformBase
{
    public FunctionalPlatform(ILogger<FunctionalPlatform> logger, PlatformConfig config) : base(logger, config)
    {
        
    }

    protected override Task MainLoop(Submission submission, string repoBasePath)
    {
        throw new NotImplementedException();
    }
}