namespace UniGrader.Shared.Models;

public class PlatformConfig
{
    /// <summary>
    /// The type of evaluation to be done against Submissions
    /// </summary>
    public Evaluation Type { get; set; }

    /// <summary>
    /// The base docker-image to use when creating Submission Docker File
    /// </summary>
    public string BaseSubmissionImage { get; set; }

    /// <summary>
    /// Version of <see cref="BaseSubmissionImage"/> to pull
    /// </summary>
    public string BaseSubmissionImageVersion { get; set; } = "latest";

    /// <summary>
    /// Grab the files with matching extensions from Output of submission docker run
    /// </summary>
    public string[] OutputExtensions { get; set; }

    /// <summary>
    /// The arguments that will be used in the docker entrypoint
    /// </summary>
    public string[] EntrypointArgs { get; set; }
}