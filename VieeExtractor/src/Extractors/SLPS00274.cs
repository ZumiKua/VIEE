using System.Text;
using BizHawk.Client.Common;

namespace VieeExtractor.Extractors;

public class SLPS00274 : IExtractor
{
    
    private static readonly Encoding ShiftJISEncoding = Encoding.GetEncoding("shift_jis");
    
    private const int TextMemoryStart = 0xD7E76;
    private const int TextMemoryLength = 0xD8206 - 0xD7E76;
    private const int LineLength = 0x4C;
    
    private readonly ApiContainer _apiContainer;
    private readonly IExtractResultListener _listener;
    private readonly StringBuilder _stringBuilder = new();
    private readonly byte[] _lineBuf = new byte[LineLength];
    
    private string _lastText = "";
     

    public SLPS00274(ApiContainer apiContainer, IExtractResultListener listener)
    {
        _apiContainer = apiContainer;
        _listener = listener;
    }

    public void FrameEnd(bool fastForward)
    {
        _stringBuilder.Clear();
        var mem = _apiContainer.Memory.ReadByteRange(TextMemoryStart, TextMemoryLength);
        for (var i = 0; i < mem.Count; i += LineLength)
        {
            var flag = mem[i] | (mem[i + 1] << 8);
            if (flag == 0)
            {
                continue;
            }

            var len = 0;
            for (var j = 2; j < LineLength; j++)
            {
                len = j - 2;
                if (mem[j + i] == 0)
                {
                    break;
                }

                _lineBuf[j - 2] = mem[j + i];
            }
            
            var line = ShiftJISEncoding.GetString(_lineBuf, 0, len);
            _stringBuilder.Append(line);
            _stringBuilder.Append('\n');
        }

        if (IsStringEqualToStringBuilder(_lastText, _stringBuilder) || fastForward)
        {
            return;
        }
        _lastText = _stringBuilder.ToString();
        _listener.OnNewData(ExtractorData.CreateText(_lastText, ""));
    }

    private static bool IsStringEqualToStringBuilder(string a, StringBuilder b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }
}