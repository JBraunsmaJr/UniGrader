using Newtonsoft.Json.Linq;
using UniGrader.Models;
using UniGrader.Shared.Models;
using MatchType = UniGrader.Models.MatchType;

namespace UniGrader.Graders;

public class QAGrader : GraderBase
{
    private static ExpectedConfig AnswerKey;
    
    public QAGrader(Language lang, string repoBase) : base(lang, repoBase)
    {
    }
    
    protected override Task<GradeResults> Grade(string data)
    {
        /*
         * It's possible that the submission outputs multiple things to console
         * We are most interested in the last line of text
         */

        GradeResults? results = new();

        if (string.IsNullOrEmpty(data))
            return Task.FromResult(results);
        
        try
        {
            string t = data.EndsWith('\n') ? data[..^1] : data;
            JObject json = JObject.Parse(t.Split('\n').Last());
            foreach (var entry in AnswerKey.Config)
            {
                // Ignore items that aren't in the answer key
                if (!json.ContainsKey(entry.Key))
                {
                    results.Wrong.Add(entry.Key, "Unanswered");
                    continue;
                }
                
                var submissionAnswer = json[entry.Key];

                var (points, wasPartial) = AnswerKey.Process(entry.Key, submissionAnswer);

                if (points is null or <= 0 || wasPartial)
                {
                    results.Wrong.Add(entry.Key, $"Submitted value was '{submissionAnswer}'");
                }
                else
                    results.Points += points.Value;
            }

            results.TotalPoints = AnswerKey.TotalPoints;
        }
        catch(Exception ex)
        {
            Errors.Add("JSON", ex.Message);
        }

        return Task.FromResult(results);
    }
    
    protected override async Task<bool> HasPrerequisites()
    {
        string keyPath = Path.Join(Util.PlatformDataPath, "answerkey.json");
        bool exists = File.Exists(keyPath);

        if (!exists)
        {
            Errors.Add("AnswerKey", "Could not locate answer key");
            return false;
        }

        if (AnswerKey == null)
        {
            string contents = await File.ReadAllTextAsync(keyPath);
            AnswerKey = new(contents);
        }
            
        return true;
    }

    
}