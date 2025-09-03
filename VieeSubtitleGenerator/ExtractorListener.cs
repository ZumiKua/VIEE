using System.IO;

namespace VieeSubtitleGenerator;

public interface IExtractorListener
{
    void OnText(string text);

    void OnChoices(string[] choices, int index);
    
    void OnError(Exception exception);
}