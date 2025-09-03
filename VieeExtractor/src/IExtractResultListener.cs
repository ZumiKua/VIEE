namespace VieeExtractor;

public interface IExtractResultListener
{
    void OnNewText(string text);

    void OnNewChoices(string[] choices, int index);
}