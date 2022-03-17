using UniGrader.Models;

namespace UniGrader.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class LanguageAttribute : Attribute
{
    public Language Language { get; }

    public LanguageAttribute(Language language)
    {
        Language = language;
    }
}