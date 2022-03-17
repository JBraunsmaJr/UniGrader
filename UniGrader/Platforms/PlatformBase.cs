using System.Management.Automation;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using UniGrader.Builders;
using UniGrader.Extensions;
using UniGrader.Models;
using UniGrader.Shared.Models;
using Stats = UniGrader.Shared.Models.Stats;

namespace UniGrader.Platforms;

/// <summary>
/// <para>Foundation for platform types provided.</para>
/// <para>Each type can have a wildly different setup that's needed in order to perform the automated grading/reporting</para>
/// </summary>
public abstract class PlatformBase
{
    protected readonly ILogger<PlatformBase> Logger;
    protected readonly PlatformConfig PlatformConfig;
    protected readonly DockerClient Client = new DockerClientConfiguration().CreateClient();
    protected Dictionary<string, Stats> UsageStats = new();

    public PlatformBase(ILogger<PlatformBase> logger, PlatformConfig config)
    {
        Logger = logger;
        PlatformConfig = config;
    }

    /// <summary>
    /// Attempts to clone repository for <paramref name="submission"/>
    /// </summary>
    /// <param name="submission">Submission to clone</param>
    /// <returns>Path to cloned repository</returns>
    /// <exception cref="Exception">When cloned repository failed to... clone</exception>
    private async Task<string> CloneRepo(Submission submission)
    {
        string cloneDir = Util.SubmissionDataPath;

        if (!Directory.Exists(cloneDir))
            Directory.CreateDirectory(cloneDir);

        string dirName = Util.LastSegmentOfUrlRegex.Match(submission.GithubUrl).Value
            .Replace(".git", "");

        await Util.ExecutePowershell($"cd \"{cloneDir}\" && git clone \"{submission.GithubUrl}\"", OnProgress, OnError);
        
        string clonedPath = Path.Join(cloneDir, dirName);

        if (!Directory.Exists(clonedPath))
            throw new Exception($"Was unable to clone '{submission.GithubUrl}' for {submission.Name}");

        return clonedPath;
    }

    /// <summary>
    /// Processes the submission.csv file - yielding current item to process
    /// </summary>
    /// <returns><see cref="Submission"/> to process</returns>
    /// <exception cref="FileNotFoundException">When csv file cannot be found at PlatformDataPath</exception>
    protected async IAsyncEnumerable<Submission> ParseSubmissions()
    {
        var file = Directory.GetFiles(Util.PlatformDataPath, "*.csv")
            .FirstOrDefault();

        if (file is null)
            throw new FileNotFoundException("Was unable to locate submission file with .csv extension");

        await using var stream = new FileStream(file, FileMode.Open);
        using var reader = new StreamReader(stream);

        string line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            var split = line.Split(',');
            yield return new Submission
            {
                Name = split[0],
                GithubUrl = split[1]
            };
        }
    }
    
    /// <summary>
    /// Logs <paramref name="e"/> to LogError stream
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected void OnError(object? sender, DataAddingEventArgs e)
    {
        if(e.ItemAdded is null)
            return;
        
        Logger.LogError(e.ItemAdded.ToString());
    }
    
    /// <summary>
    /// Logs <paramref name="e"/> to LogInfo stream
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected void OnProgress(object? sender, DataAddingEventArgs e)
    {
        if (e.ItemAdded is null)
            return;

        Logger.LogInformation(e.ItemAdded?.ToString());
    }

    protected virtual async Task Initialize()
    {
        await Cleanup(Util.SubmissionDataPath);
        await Cleanup(Util.LogsPath);
        Directory.CreateDirectory(Util.LogsPath);
    }

    protected abstract Task MainLoop(Submission submission, string repoBasePath);

    /// <summary>
    /// Continuously retrieves docker statistics until the container stops running
    /// </summary>
    /// <param name="submission"></param>
    /// <param name="containerResponse"></param>
    /// <param name="source"></param>
    protected virtual async Task GetContainerStats(Submission submission, CreateContainerResponse containerResponse, CancellationTokenSource source)
    {
        try
        {
            await Client.Containers.GetContainerStatsAsync(containerResponse.ID, new()
            {
                Stream = true
            }, ContainerProgress(submission, source), source.Token);
        }
        catch
        {
            // Ignored
        }
    }
    
    /// <summary>
    /// <para>Determines <paramref name="repoPath"/>'s programming language.</para>
    /// <para>Creates dockerfile and docker image for <paramref name="repoPath"/></para>
    /// <para>Creates container (but doesn't start it)</para>
    /// </summary>
    /// <param name="submission"></param>
    /// <param name="repoPath"></param>
    /// <returns></returns>
    protected async Task<(Language language, string dockerPath, CreateContainerResponse containerResponse)> PrepRepository(
        Submission submission, string repoPath)
    {
        Language lang = Util.DetermineLanguageOfDir(repoPath);
        
        Logger.LogInformation("Building Docker Image for: {Name}", submission.Name);
        var dockerFile = await new DockerFileBuilder(lang, repoPath).Create(PlatformConfig);
        await CreateDockerImage(repoPath, dockerFile, submission.SanitizedName.ToLower() + ":latest");

        var containerResponse = await Client.Containers.CreateContainerAsync(new()
        {
            Image = submission.SanitizedName.ToLower() + ":latest",
            AttachStdout = true
        });

        return (lang, dockerFile, containerResponse);
    }

    protected Progress<ContainerStatsResponse> ContainerProgress(Submission submission,
        CancellationTokenSource tokenSource) => new(response =>
    {
        // We shall track progress only up until usage == 0 (means container stopped)
        if (response.CPUStats.CPUUsage.TotalUsage <= 0)
        {
            tokenSource.Cancel();
            return;
        }

        #region Calculate and store stats
        double cpuUsage = response.CPUStats.CPUUsage.TotalUsage * 1.0 / response.CPUStats.SystemUsage * 1.0 * 100;
        double memoryUsage = response.MemoryStats.Usage * 1.0 / response.MemoryStats.Limit * 1.0 * 100;

        UsageStats.Add(submission.Name, new()
        {
            CpuUsage = cpuUsage,
            MemoryUsage = memoryUsage,
            Name = submission.Name
        });
        #endregion
        
        #region Display Statistics to log
        StringBuilder sb = new();
        
        sb.AppendLine(response.CPUStats.ToTable(
            new("System Usage", stats => stats.SystemUsage), 
            new("Total Usage", stats => stats.CPUUsage.TotalUsage), 
            new("Online CPUs", stats => stats.OnlineCPUs),
            new("%", _ => Util.AsPercent(cpuUsage))));
        sb.AppendLine();
        sb.AppendLine(response.MemoryStats.ToTable(new (string header, Func<MemoryStats, object> value)[]
        {
            new("Limit", stats => stats.Limit),
            new("Usage", stats => stats.Usage),
            new("Max Usage", stats => stats.MaxUsage),
            new("%", _ => Util.AsPercent(memoryUsage))
        }));
                    
        sb.AppendLine();
        Logger.LogWarning(sb.ToString());
        #endregion
    });

    private async Task CreateDockerImage(string repoPath, string dockerFile, string tag)
    {
        await Util.ExecutePowershell($"cd \"{repoPath}\" && docker build -f \"{dockerFile}\" -t {tag} .", OnProgress,
            OnProgress);
    }
    
    public async Task Run()
    {
        await Initialize();

        string repoBasePath = string.Empty;
        await foreach (var submission in ParseSubmissions())
        {
            repoBasePath = await CloneRepo(submission);
            Logger.LogInformation("Cloned {Name}'s repo '{Path}'", submission.Name, repoBasePath);
            
            try
            {
                await MainLoop(submission, repoBasePath);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
            finally
            {
                await Cleanup(repoBasePath);
            }
        }
    }
    
    /// <summary>
    /// Runs some basic cleanup
    /// </summary>
    /// <param name="repoPath"></param>
    protected async Task Cleanup(string repoPath)
    {
        if(!string.IsNullOrEmpty(repoPath))
            await Util.DeleteDir(repoPath);
    }

    /// <summary>
    /// Runs cleanup on submission / container / image
    /// </summary>
    /// <param name="submission"></param>
    /// <param name="repoPath"></param>
    /// <param name="containerId"></param>
    protected async Task Cleanup(Submission? submission, string repoPath, string containerId)
    {
        await Cleanup(repoPath);

        if (!string.IsNullOrEmpty(containerId))
        {
            Logger.LogInformation("Cleaning up container...");
            await Util.ExecutePowershell($"docker rm -f {containerId}", OnProgress, OnError);    
        }

        if (submission is not null)
        {
            Logger.LogInformation("Cleaning up image...");
            await Client.Images.DeleteImageAsync(submission.SanitizedName.ToLower(), new()
            {
                Force = true
            });    
        }
    }
}