namespace UniGrader.TestFramework;

/*
    This test framework needs to work in the context of someone else's project.
    
    The methodologies behind adding/inserting your code into this foreign project
    will vary language to language.
    
    In Python we could potentially insert your test-cases at the bottom of their main.py / app.py file and run it
    In a compiled language we'd have to be more clever about how/where code gets inserted and tied together.
 */

public abstract class TestFrameworkBase
{
    protected IConfiguration Configuration { get; }

    public TestFrameworkBase(IConfiguration config)
    {
        Configuration = config;
    }
    
    public abstract Task InitializeContext(string repoBase);
    public abstract bool Execute();
}