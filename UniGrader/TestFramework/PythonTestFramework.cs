namespace UniGrader.TestFramework;

public class PythonTestFramework : TestFrameworkBase
{
    public PythonTestFramework(IConfiguration config) : base(config)
    {
        
    }
    
    /*
        We need to pull user content from a configurable location
        
        data/frameworks/language
        Can navigate into a subdirectory where directory name equals language we want to use
        
        For Python:
        data/frameworks/python
     */
    
    public override Task InitializeContext(string repoBase)
    {
        throw new NotImplementedException();
    }

    public override bool Execute()
    {
        throw new NotImplementedException();
    }
}