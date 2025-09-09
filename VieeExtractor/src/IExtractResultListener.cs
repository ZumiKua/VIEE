namespace VieeExtractor;

public interface IExtractResultListener
{

    void OnNewData(ExtractorData data);
    
}