using UniGrader.Graders;
using UniGrader.Shared.Models;

namespace UniGrader.Platforms;

public class QaPlatform : PlatformBase
{
    private Dictionary<string, GradeResults> _grades = new ();
    
    public QaPlatform(ILogger<QaPlatform> logger, PlatformConfig config) : base(logger, config)
    {
    }

    protected override async Task MainLoop(Submission submission, string repoBasePath)
    {
        var repoInfo = await PrepRepository(submission, repoBasePath);

        // This will be utilized for stopping container stats collection -- otherwise it goes on forever
        CancellationTokenSource cts = new();
        
        DateTime start = DateTime.Now;
        await Client.Containers.StartContainerAsync(repoInfo.containerResponse.ID, new());
        var attachResponse = await Client.Containers.AttachContainerAsync(repoInfo.containerResponse.ID, false, new()
        {
            Stdout = true,
            Stderr = true,
            Stream = true
        });
            
        await GetContainerStats(submission, repoInfo.containerResponse, cts);

        var (stdout, stderr) = await attachResponse.ReadOutputToEndAsync(default);
        DateTime end = DateTime.Now;
        Logger.LogWarning("Time took: {Time}", $"{(end-start):g}");

        if (!string.IsNullOrEmpty(stderr))
            Logger.LogError(stderr);
        
        // Processing the projects output
        if (!string.IsNullOrEmpty(stdout))
        {
            await using MemoryStream memoryStream = new();
            await using TextWriter writer = new StreamWriter(memoryStream);
            await writer.WriteAsync(stdout);
            await writer.FlushAsync();

            var grader = new QAGrader(repoInfo.language, repoBasePath);
            var results = await grader.Run(stdout);

            if (grader.Success)
                _grades[submission.Name] = results;
            else
                Logger.LogCritical(string.Join("\n", grader.Errors.Select(x => $"{x.Key}: {x.Value}")));

            await using var fileStream =
                new FileStream(Path.Join(Util.LogsPath, $"{submission.Name}-logs.txt"), FileMode.Create);
            var array = memoryStream.ToArray();
            await fileStream.WriteAsync(array, 0, array.Length);
            await fileStream.FlushAsync();
        }
        else
            Logger.LogError("{Name} -- not stdout found", submission.Name);

        await Cleanup(submission, repoBasePath, repoInfo.containerResponse.ID);
    }
}