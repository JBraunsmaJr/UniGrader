using Newtonsoft.Json.Linq;
using MatchType = UniGrader.Shared.Models.MatchType;

namespace UniGrader.Models;

public class ExpectedConfig
{
    public readonly JObject Config;
    public double TotalPoints { get; private set; }

    public ExpectedConfig(string json)
    {
        Config = JObject.Parse(json);
        CalculateAllPoints();
    }

    /// <summary>
    /// Checks if token is of type number
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private bool IsNumber(JToken token) => token.Type == JTokenType.Float || token.Type == JTokenType.Integer;
    
    /// <summary>
    /// Checks if two types are a number or at least equal to each other
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns>If types are 'matching'</returns>
    private bool AreMatchingTypes(JToken left, JToken right) => left.Type == right.Type || IsNumber(left) && IsNumber(right);

    /// <summary>
    /// Does the configuration contain <paramref name="key"/>
    /// </summary>
    /// <param name="key"></param>
    /// <returns>True if config contains key, otherwise false</returns>
    public bool ContainsKey(string key) => Config.ContainsKey(key);

    private void CalculateAllPoints()
    {
        TotalPoints = 0;
        foreach (var entry in Config)
        {
            var rewards = GetPoints(Config[entry.Key]["points"]);
            TotalPoints += rewards.full;
        }
    }
    
    /// <summary>
    /// Compare <paramref name="submittedValue"/> against given <paramref name="key"/>
    /// </summary>
    /// <param name="key">Key from <see cref="Config"/> to check</param>
    /// <param name="submittedValue">Value to compare against</param>
    /// <returns>Number of points to reward for given answer</returns>
    public (double? points, bool wasPartial) Process(string key, JToken submittedValue)
    {
        // Can't grade something we don't have answers to
        if (!Config.ContainsKey(key))
            return (null, false);
        
        var rewards = GetPoints(Config[key]["points"]);

        var expectedValue = Config[key]["expected"];
        
        // No complicated partial credit stuff going on here
        if (AreMatchingTypes(submittedValue, expectedValue))
        {
            var isCorrect = IsCorrect(submittedValue, expectedValue, GetMatchType(Config[key]));

            if (isCorrect)
                return (rewards.full, false);
            
            return (0, false);
        }

        // If the token types are not the same, we're expecting the configuration to be a dictionary
        if (Config[key].Type != JTokenType.Object)
            return (0, false);

        var expectedFullEntry = expectedValue["full"];
        var expectedPartialEntry = expectedValue["partial"];

        if (AreMatchingTypes(submittedValue, expectedFullEntry) && IsCorrect(submittedValue, expectedFullEntry, 
                GetMatchType(expectedFullEntry)))
            return (rewards.full, false);

        if (AreMatchingTypes(submittedValue, expectedPartialEntry) && IsCorrect(submittedValue, expectedPartialEntry,
                GetMatchType(expectedPartialEntry)))
            return (rewards.partial, true);
        
        if(AreMatchingTypes(submittedValue, expectedFullEntry["expected"]) && 
           IsCorrect(submittedValue, expectedFullEntry["expected"], GetMatchType(expectedFullEntry)))
            return (rewards.full, false);

        if (AreMatchingTypes(submittedValue, expectedPartialEntry["expected"]) &&
            IsCorrect(submittedValue, expectedPartialEntry["expected"], GetMatchType(expectedPartialEntry)))
            return (rewards.partial, true);
        
        return (0, false);
    }

    /// <summary>
    /// Extracts the 'MatchType' from <paramref name="token"/> if it exists
    /// Otherwise defaults to None
    /// </summary>
    /// <param name="token"></param>
    /// <returns>MatchType from token if it exists</returns>
    private MatchType GetMatchType(JToken token)
    {
        if (!token.HasValues)
            return MatchType.None;
        
        if (token.SelectToken("matchType") != null)
            return token["matchType"].Value<MatchType>();
        return MatchType.None;
    }
    
    /// <summary>
    /// checks to see if two token values are the same
    /// </summary>
    /// <param name="submissionToken"></param>
    /// <param name="expectedToken"></param>
    /// <param name="matchType"></param>
    /// <returns></returns>
    private bool IsCorrect(JToken submissionToken, JToken expectedToken, MatchType matchType = MatchType.None)
    {
        if (expectedToken.Type == JTokenType.String)
            return expectedToken.Value<string>() == submissionToken.Value<string>();

        if (expectedToken.Type == JTokenType.Float ||
            expectedToken.Type == JTokenType.Integer)
            return expectedToken.Value<double>() == submissionToken.Value<double>();

        if (expectedToken.Type == JTokenType.Array)
        {
            var expectedValue = expectedToken.Value<object[]>();
            var submittedValue = submissionToken.Value<object[]>();

            return Util.MatchArray(submittedValue, expectedValue, matchType);
        }
        
        return false;
    }
    
    private (double full, double partial, bool isPartial) GetPoints(JToken entry)
    {
        if (entry.Type == JTokenType.Object)
        {
            double full = entry["full"].ToObject<double>();
            double partial = entry["partial"].ToObject<double>();

            return (full, partial, true);
        }

        if (entry.Type == JTokenType.Float || entry.Type == JTokenType.Integer)
        {
            double value = entry.ToObject<double>();
            return (value, value, false);
        }

        return (0, 0, false);
    }
}