namespace UniGrader.Models;

public class TestResultModel
{
    /// <summary>
    /// Name of function that was tested
    /// </summary>
    public string FunctionName { get; set; }
    
    /// <summary>
    /// Parameters that were provided
    /// </summary>
    public string[] Parameters { get; set; }
    
    /// <summary>
    /// User's output
    /// </summary>
    public string Output { get; set; }
    
    /// <summary>
    /// Did test pass
    /// </summary>
    public bool Success { get; set; }
}