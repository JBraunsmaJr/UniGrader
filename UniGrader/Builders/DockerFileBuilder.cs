using UniGrader.Models;

namespace UniGrader.Builders;

public class DockerFileBuilder
{
    public Language Lang { get; }
    public string Repo { get; }
    private string _dockerFileContents = string.Empty;

    private const string InstallDependenciesVar = "INSTALL_DEPENDENCIES";
    private const string EntrypointArgsVar = "ENTRYPOINT_ARGS";
    private const string ImageVar = "IMAGE";
    private const string ImageVersionVar = "IMAGE_VERSION";
    private const string RepoDirVar = "REPO_DIR";
    private const string EntrypointFileVar = "ENTRYPOINT_FILE";
    
    public DockerFileBuilder(Language lang, string repoPath)
    {
        Lang = lang;
        Repo = repoPath;
    }

    /// <summary>
    /// Retrieve the dockerfile path that should be utilized based on <see cref="Lang"/>
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException">Dockerfile template does not exist for language yet</exception>
    string GetDockerTemplate()
    {
        return Lang switch
        {
            Language.Python => Path.Join(Util.TemplatesPath, "Dockerfile-python.txt"),
            Language.CSharp => Path.Join(Util.TemplatesPath, "Dockerfile-csharp.txt"),
            _ => throw new NotImplementedException($"No dockerfile template exists for {Lang} yet")
        };
    }
    
    public async Task<string> Create(PlatformConfig config)
    {
        _dockerFileContents = await File.ReadAllTextAsync(GetDockerTemplate());

        List<string> entrypointArgs = config.EntrypointArgs.ToList();
        
        for(int i = 0; i < entrypointArgs.Count; i++)
            if (entrypointArgs[i].IsSubVariable() && Lang == Language.Python)
                entrypointArgs[i] = entrypointArgs[i]
                    .UpdateVar(EntrypointFileVar, await Util.GetPythonEntrypointFile(Repo));
        
        switch (Lang)
        {
            case Language.Python:
                if (File.Exists(Path.Join(Repo, "requirements.txt")))
                    _dockerFileContents = _dockerFileContents.UpdateVar(InstallDependenciesVar,
                        "COPY requirements.txt requirements.txt\n" +
                            "RUN pip3 install -r requirements.txt");
                else
                    _dockerFileContents = _dockerFileContents.UpdateVar(RepoDirVar, new DirectoryInfo(Repo).Name)
                        .UpdateVar(InstallDependenciesVar, "");
                break;
        }

        string args = string.Join(", ", entrypointArgs.Select(x=>$"\"{x}\""));

        _dockerFileContents = _dockerFileContents
            .UpdateVar(ImageVar, config.BaseSubmissionImage)
            .UpdateVar(ImageVersionVar, config.BaseSubmissionImageVersion)
            .UpdateVar(EntrypointArgsVar, args);

        string dockerFilePath = Path.Join(Repo, "Dockerfile");
        await File.WriteAllTextAsync(dockerFilePath, _dockerFileContents);

        return dockerFilePath;
    }
}