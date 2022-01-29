using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;
using System.IO;
using UniGrader.Models;

namespace UniGrader.Tests;

public class RegexTests
{
    const string TestOutputJson = "{\"question 1\": \"ale\", \"question 2\": 60.0, \"question 3\": \"ale\", \"question 4\": \"ale\"}";
    
    [Fact]
    public void TestAnswerKey()
    {
        var fileContents = File.ReadAllText(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "TestKey.json"));
        ExpectedConfig config = new ExpectedConfig(fileContents);
        
        var submissionConfig = JObject.Parse(TestOutputJson);

        var (response, partial) = config.Process("question 1", submissionConfig["question 1"]);
        Assert.Equal(1, response);

        (response, partial) = config.Process("question 2", submissionConfig["question 2"]);
        Assert.Equal(1, response);

        (response, partial) = config.Process("question 3", submissionConfig["question 3"]);
        Assert.Equal(2, response);
        
        (response, partial) = config.Process("question 4", submissionConfig["question 4"]);
        Assert.Equal(5, response);
        
        Assert.Equal(12, config.TotalPoints);
    }

    [Fact]
    public void TestSplit()
    {
        string text = "\nthis is\nsomething awesome\n";

        var split = text.Split('\n');

    }
    
    [Fact]
    public void TestSubVar()
    {
        Assert.True("%SUB%".IsSubVariable());
        Assert.False("%SUB".IsSubVariable());
        Assert.False("SUB%".IsSubVariable());
    }

    [Fact]
    public void TestUrlRegex()
    {
        Regex url = new("[^/]+(?=/$|$)");

        string repo = @"https://github.com/Aiko-IT-Systems/DisCatSharp";
        var result = url.Match(repo);

        Assert.True(result.Value.Equals("DisCatSharp"));
    }
}