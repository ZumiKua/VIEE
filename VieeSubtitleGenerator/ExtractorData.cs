namespace VieeSubtitleGenerator;
public struct ExtractorData
{
    public const int TypeText = 0;
    public const int TypeChoices = 1;
    
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
}