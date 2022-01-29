using MudBlazor;
using MudBlazor.Utilities;

namespace UniGrader.Dashboard;

public static class Util
{
    public static Color GetLetterColorClass(double grade)
    {
        return grade switch
        {
            >= .90 => Color.Success,
            >= .80 => Color.Info,
            >= .70 => Color.Secondary,
            >= .60 => Color.Warning,
            _ => Color.Error
        };
    }
    public static char GetLetterGrade(double grade)
    {
        return grade switch
        {
            >= .90 => 'A',
            >= .80 => 'B',
            >= .70 => 'C',
            >= .60 => 'D',
            _ => 'F'
        };
    }
}