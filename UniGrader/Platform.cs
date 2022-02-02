using System.Management.Automation;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json;
using UniGrader.Builders;
using UniGrader.Extensions;
using UniGrader.Graders;
using UniGrader.Models;
using UniGrader.Shared.Models;
using Stats = UniGrader.Shared.Models.Stats;

namespace UniGrader;

public class Platform
{
    private readonly ILogger<Platform> _logger;
    private readonly PlatformConfig _config;
    private readonly DockerClient _client = new DockerClientConfiguration().CreateClient();
    private Dictionary<string, GradeResults> _grades = new();
    private Dictionary<string, Stats> _stats = new();

    public Platform(ILogger<Platform> logger, PlatformConfig config)
    {
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Processes the submission.csv file - yielding current item to process
    /// </summary>
    /// <returns><see cref="Submission"/> to process</returns>
    /// <exception cref="FileNotFoundException">When csv file cannot be found at PlatformDataPath</exception>
    private async IAsyncEnumerable<Submission> ParseSubmissions()
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
    
    public async Task Run()
    {
        // Ensure our submission data is clean prior to starting
        await Cleanup(Util.SubmissionDataPath);
        await Cleanup(Util.LogsPath);

        if (!Directory.Exists(Util.LogsPath))
            Directory.CreateDirectory(Util.LogsPath);
        
        await foreach (var submission in ParseSubmissions())
        {
            _logger.LogInformation($"Processing: {submission}");
            string repoBasePath = string.Empty;
            
            try
            {
                repoBasePath = await CloneRepo(submission);
                
                _logger.LogInformation($"Cloned {submission.Name}'s repo '{repoBasePath}'");
                Language repoLang = Util.DetermineLanguageOfDir(repoBasePath);
                var dockerFile = await new DockerFileBuilder(repoLang, repoBasePath).Create(_config);
                
                _logger.LogInformation($"Building docker image for: {submission.Name}...");
                await CreateDockerImage(repoBasePath, dockerFile, submission.SanitizedName.ToLower() + ":latest");

                var containerResponse = await _client.Containers.CreateContainerAsync(new()
                {
                    Image = submission.SanitizedName.ToLower() + ":latest",
                    AttachStdout = true
                });
                
                // Shall be utilized to stop the container stats task (for some reason doesn't stop when container stops)
                CancellationTokenSource cts = new();
                
                Progress<ContainerStatsResponse> statsProgress = new Progress<ContainerStatsResponse>(response =>
                {
                    // We shall track progress only up until usage == 0 (means container stopped)
                    if (response.CPUStats.CPUUsage.TotalUsage <= 0)
                    {
                        cts.Cancel();
                        return;
                    }

                    double cpuUsage = response.CPUStats.CPUUsage.TotalUsage * 1.0 / response.CPUStats.SystemUsage * 1.0 * 100;
                    double memoryUsage = response.MemoryStats.Usage * 1.0 / response.MemoryStats.Limit * 1.0 * 100;
                    
                    _stats.Add(submission.Name, new()
                    {
                        CpuUsage = cpuUsage,
                        MemoryUsage = memoryUsage,
                        Name = submission.Name
                    });
                    
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
                    _logger.LogWarning(sb.ToString());
                });

                DateTime start = DateTime.Now;
                await _client.Containers.StartContainerAsync(containerResponse.ID, new());
                
                var attachResponse = await _client.Containers.AttachContainerAsync(containerResponse.ID, false, new()
                {
                    Stdout = true, Stderr = true, Stream = true
                });

                try
                {
                    await _client.Containers.GetContainerStatsAsync(containerResponse.ID, new ContainerStatsParameters
                    {
                        Stream = true
                    }, statsProgress, cts.Token);
                }
                catch
                {
                    // ignored
                }

                var (stdout, stderr) = await attachResponse.ReadOutputToEndAsync(default);

                DateTime end = DateTime.Now;
                _logger.LogWarning($"Time took: {(end-start):g}");
                
                if (!string.IsNullOrEmpty(stderr))
                    _logger.LogError(stderr);
                
                // Processing the project's output
                if (!string.IsNullOrEmpty(stdout))
                {
                    await using MemoryStream memoryStream = new();
                    await using TextWriter writer = new StreamWriter(memoryStream);
                    await writer.WriteAsync(stdout);
                    await writer.FlushAsync();
                    
                    var grader = GetGrader(repoLang, repoBasePath);
                    var results = await grader.Run(stdout);
                    
                    if (grader.Success)
                        _grades[submission.Name] = results;
                    else
                        _logger.LogCritical(string.Join("\n",grader.Errors.Select(x=>$"{x.Key}: {x.Value}")));
                    
                    await using var fileStream =
                        new FileStream(Path.Join(Util.LogsPath, $"{submission.Name}-logs.txt"), FileMode.Create);
                    var array = memoryStream.ToArray();
                    await fileStream.WriteAsync(array, 0, array.Length);
                    await fileStream.FlushAsync();
                }
                else
                    _logger.LogWarning($"{submission.Name} -- no stdout found");

                _logger.LogInformation("Cleaning up container...");
                await Util.ExecutePowershell($"docker rm -f {containerResponse.ID}", OnProgress, OnError);
                
                _logger.LogInformation("Cleaning up image...");
                await _client.Images.DeleteImageAsync(submission.SanitizedName.ToLower(), new()
                {
                    Force = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            finally
            {
                await Cleanup(repoBasePath);    
            }
        }

        _logger.LogInformation("Generating reports...");
        string reportJson = JsonConvert.SerializeObject(_grades);
        await File.WriteAllTextAsync(Path.Join(Util.SubmissionDataPath, "report.json"), reportJson);

        string usageReport = JsonConvert.SerializeObject(_stats.Values.ToArray());
        await File.WriteAllTextAsync(Path.Join(Util.SubmissionDataPath, "usage-stats.json"), usageReport);
    }

    /// <summary>
    /// Get the grader for specified type and repo -- based on Framework
    /// </summary>
    /// <param name="lang"></param>
    /// <param name="repo"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private GraderBase GetGrader(Language lang, string repo)
    {
        switch (_config.Type)
        {
            case Evaluation.QuestionAnswer:
                return new QAGrader(lang, repo);
            default:
                throw new NotImplementedException(lang.ToString());
        }
    }
    
    private async Task CreateDockerImage(string repoPath, string dockerFile, string tag)
    {
        await Util.ExecutePowershell($"cd \"{repoPath}\" && docker build -f \"{dockerFile}\" -t {tag} .", OnProgress, OnProgress);
    }

    void OnError(object? sender, DataAddingEventArgs e)
    {
        if(e.ItemAdded is null)
            return;
        
        _logger.LogError(e.ItemAdded.ToString());
    }
    
    void OnProgress(object? sender, DataAddingEventArgs e)
    {
        if (e.ItemAdded is null)
            return;

        _logger.LogInformation(e.ItemAdded.ToString());
    }

    private async Task Cleanup(string repoPath)
    {
        if(!string.IsNullOrEmpty(repoPath))
            await Util.DeleteDir(repoPath);
    }
}