using BizHawk.Client.Common;
using VieeExtractor.Extractors;

namespace VieeExtractor;

public class ExtractorFactory
{
    public static IExtractor Create(string? hash, ApiContainer apiContainer, IExtractResultListener listener)
    {
        switch (hash)
        {
            case "12CFF376":
                return new SLPS02542(apiContainer, listener);
            case "5DCB56C1":
                return new SLPS00274(apiContainer, listener);
            case "37946519":
            case "9A49C0E4":
                return new SLPS03015(hash, apiContainer, listener);
            default:
                return DummyExtractor.Instance;
        }
    }
}