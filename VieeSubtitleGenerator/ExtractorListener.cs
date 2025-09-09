namespace VieeSubtitleGenerator;

public interface IExtractorListener
{
    void OnText(string text, string speaker);

    void OnChoices(string[] choices, int index);
    
    void OnError(Exception exception);
}