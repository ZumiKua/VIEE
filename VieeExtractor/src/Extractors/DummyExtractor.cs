namespace VieeExtractor.Extractors;

public class DummyExtractor : IExtractor
{
    
    public static readonly DummyExtractor Instance = new DummyExtractor(); 
    
    public void FrameEnd(bool fastForward)
    {
        
    }
}