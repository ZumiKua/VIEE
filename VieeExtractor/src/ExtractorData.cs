using System;

namespace VieeExtractor;

public struct ExtractorData
{
    public int Type { get; set; }
    public string Text { get; set; }
    public string[] Choices { get; set; }
    public int Index { get; set; }

    private ExtractorData(int type, string text, string[] choices, int index)
    {
        Type = type;
        Text = text;
        Choices = choices;
        Index = index;
    }

    public static ExtractorData CreateText(string text)
    {
        return new ExtractorData(0, text, Array.Empty<string>(), -1);
    }

    public static ExtractorData CreateChoices(string[] choices, int index)
    {
        return new ExtractorData(1, "", choices, index);
    }
}