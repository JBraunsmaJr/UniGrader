using UniGrader.Shared.Models;

namespace UniGrader.Dashboard;

public static class TestData
{
    private static Random _random = new();

    private static Dictionary<string, Tuple<string[], int, int>> TestKey => new()
    {
        {"Question 1", new Tuple<string[], int, int>(new[] {"Sword", "Ale", "Health Potion"}, _random.Next(0, 3), 1)},
        { "Question 2", new Tuple<string[], int, int>(new[]{"60", "30", "20", "1"}, _random.Next(0, 4), 2)},
        { "Question 3", new Tuple<string[], int, int>(new[]{"Bow","Arrows","Food","Ale"}, _random.Next(0,4), 3)}
    };

    public static (string name, GradeResults results) GetTestData()
    {
        string name = $"Person {_random.Next(0, 1000)}";

        GradeResults results = new();

        foreach (var item in TestKey)
        {
            int randomIndex = _random.Next(0, item.Value.Item1.Length);

            if (randomIndex == item.Value.Item2)
                results.Points += item.Value.Item3;
            else
                results.Wrong.Add(item.Key, $"Submitted value '{item.Value.Item1[randomIndex]}'");
            results.TotalPoints += item.Value.Item3;
        }
        
        return (name, results);
    }
}