using System.Collections.Generic;
using System.Text;
using BizHawk.Client.Common;

namespace VieeExtractor.Extractors;

public class SLPS01030 : IExtractor
{
    private const uint TextBaseAddress = 0x158000;
    private const uint IndexOffsetFromFrameStart = 0x38;
    private const int MaxReadLength = 0x400;
    
    private static readonly Encoding ShiftJISEncoding = Encoding.GetEncoding("shift_jis");
    private static readonly Dictionary<uint, uint> RetAddrToOffset = new()
    {
        { 0x80060618u, 4 },
        { 0x80060620u, 4 },
        { 0x8006069Cu, 4 },
        { 0x800606ACu, 4 },
        { 0x800606B4u, 4 },
        { 0x800606D4u, 4 },
        { 0x800607ACu, 4 },
        { 0x80060804u, 8 },
        { 0x80060834u, 8 },
        { 0x8006083Cu, 8 },
        { 0x80060814u, 4 },
    };
    
    private readonly ApiContainer _apiContainer;
    private readonly IExtractResultListener _listener;
    private readonly HashSet<uint> _addresses = new();
    private readonly byte[] _textBuf = new byte[MaxReadLength];
    private uint _last;
    private uint? _lastIndex = null;

    public SLPS01030(ApiContainer apiContainer, IExtractResultListener listener)
    {
        _apiContainer = apiContainer;
        _listener = listener;
    }

    public void FrameEnd(bool fastForward)
    {
        var d = _apiContainer.Memory.ReadU32(0x1ffd84);
        var stacks = _apiContainer.Memory.ReadByteRange(0x1FEFF0, 0x1000);
        uint retAddr = 0;
        uint stackAddr = 0;
        for (var i = 0; i < stacks.Count; i += 4)
        {
            long h3 = stacks[i + 3];
            long h2 = stacks[i + 2];
            long h1 = stacks[i + 1];
            long h0 = stacks[i + 0];
            var addr = (uint)(h0 | (h1 << 8) | (h2 << 16) | (h3 << 24));
            if (RetAddrToOffset.ContainsKey(addr))
            {
                stackAddr = (uint)i + 0x801FEFF0u;
                retAddr = addr;
                break;
            }
        }

        if (_last == retAddr)
        {
            return;
        }
        _last = retAddr;
        if (_last == 0)
        {
            _listener.OnNewData(ExtractorData.CreateText("", ""));
            return;
        }

        var indexAddr = IndexOffsetFromFrameStart + RetAddrToOffset[retAddr] + stackAddr;
        var index = _apiContainer.Memory.ReadU32(indexAddr);
        if (index <= 0)
        {
            return;
        }
        index -= 1;
        if (_lastIndex == index)
        {
            return;
        }
        _lastIndex = index;
        var offset1 = _apiContainer.Memory.ReadU16(TextBaseAddress);
        var offset2 = _apiContainer.Memory.ReadU16(TextBaseAddress + offset1 + index * 2);
        var textAddr = offset2 + TextBaseAddress;
        var bytes = _apiContainer.Memory.ReadByteRange(textAddr, MaxReadLength);
        var len = 0;
        for (var i = 0; i + 1 < bytes.Count; i += 2)
        {
            var c = bytes[i];
            var c1 = bytes[i + 1];
            if (c == 'c')
            {
                continue;
            }

            if (c == 0)
            {
                break;
            }
            _textBuf[len] = c;
            _textBuf[len + 1] = c1;
            len += 2;
        }

        var str = ShiftJISEncoding.GetString(_textBuf, 0, len);
        _listener.OnNewData(ExtractorData.CreateText(str, ""));

    }
}