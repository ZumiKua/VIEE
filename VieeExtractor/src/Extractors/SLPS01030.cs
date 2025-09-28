using System;
using System.Collections.Generic;
using System.Text;
using BizHawk.Client.Common;

namespace VieeExtractor.Extractors;

public class SLPS01030 : IExtractor
{
    private const uint TalkStateAddr = 0x88c9c;
    private const uint NonTalkValue = 0x80090348;
    private const uint TalkContentAddr = 0x90348;
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
    private readonly byte[] _textBuf = new byte[MaxReadLength];
    private uint _lastRetAddr;
    private int? _lastIndex = null;
    private string _lastTalk = "";
    private string _lastCutscene = "";

    public SLPS01030(ApiContainer apiContainer, IExtractResultListener listener)
    {
        _apiContainer = apiContainer;
        _listener = listener;
    }

    private void SkipControlNumber(IReadOnlyList<byte> bytes,ref int i)
    {
        while (bytes[i] < 0x3A)
        {
            ++i;
        }

        if (bytes[i] == 0x24)
        {
            ++i;
        }
    }

    private string GetTalkString()
    {
        var value = _apiContainer.Memory.ReadU32(TalkStateAddr);
        if (value == NonTalkValue || (value & 0xFF000000) != 0x80000000)
        {
            return "";
        }
        Console.WriteLine($"Value {value:X}");

        var bytes = _apiContainer.Memory.ReadByteRange(TalkContentAddr, MaxReadLength);
        var len = 0;
        for (var i = 0; i < bytes.Count; i++)
        {
            var c = bytes[i];
            var chigh = c & 0xF0;
            if (c == 0x5c)
            {
                i += 2;
                switch (bytes[i - 1])
                {
                    case 0x58:
                    case 0x59:
                    case 0x63:
                    case 0x69:
                    case 0x74:
                    case 0x75:
                    case 0x78:
                    case 0x79:
                        SkipControlNumber(bytes, ref i);
                        break;
                    case 0x6e:
                        _textBuf[len++] = 0x0A;
                        break;
                }
                //for loop will add another one to i.
                i -= 1;
            }
            else if (chigh == 0x80 || chigh == 0x90 || chigh == 0xE0)
            {
                _textBuf[len] = c;
                _textBuf[len + 1] = bytes[i + 1];
                len += 2;
                i += 1;
            }
            else if (c == 0)
            {
                break;
            }
            else
            {
                _textBuf[len++] = c;
            }
        }

        var str = ShiftJISEncoding.GetString(_textBuf, 0, len);
        return str;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="fastForward"></param>
    /// <param name="outStr">currently displaying cutscene string. Will be assigned when return value is true.</param>
    /// <returns>does cutscene string changed.</returns>
    public bool CheckCutSceneString(bool fastForward, ref string outStr)
    {
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

        if (_lastRetAddr == retAddr)
        {
            return false;
        }
        _lastRetAddr = retAddr;
        if (_lastRetAddr == 0)
        {
            outStr = "";
            return true;
        }

        var indexAddr = ConvertMemoryAddress(IndexOffsetFromFrameStart + RetAddrToOffset[retAddr] + stackAddr);
        var index = _apiContainer.Memory.ReadS32(indexAddr) - 1;
        Console.WriteLine($"IndexAddress: {indexAddr:X} Index {index}");
        if (_lastIndex == index)
        {
            return false;
        }
        if (index < 0)
        {
            outStr = "";
            return true;
        }
        _lastIndex = index;
        if (fastForward)
        {
            outStr = "";
            return true;
        }
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

            if (c == ' ')
            {
                _textBuf[len++] = c;
                i -= 1;
                continue;
            }

            if (c == 0x0D)
            {
                _textBuf[len++] = 0x0A;
                i -= 1;
                continue;
            }
            _textBuf[len] = c;
            _textBuf[len + 1] = c1;
            len += 2;
        }

        var str = ShiftJISEncoding.GetString(_textBuf, 0, len);
        outStr = str;
        return true;

    }

    public void FrameEnd(bool fastForward)
    {
        var cutSceneChanged = CheckCutSceneString(fastForward, ref _lastCutscene);
        if (!string.IsNullOrEmpty(_lastCutscene))
        {
            if (cutSceneChanged)
            {
                _listener.OnNewData(ExtractorData.CreateText(_lastCutscene, ""));
            }
            _lastTalk = "";
        }
        else
        {
            var talk = GetTalkString();
            if (talk != _lastTalk)
            {
                _lastTalk = talk;
                _listener.OnNewData(ExtractorData.CreateText(_lastTalk, ""));
            }
            else if (cutSceneChanged)
            {
                _listener.OnNewData(ExtractorData.CreateText(_lastCutscene, ""));
            }
        }
    }
    
    private static long ConvertMemoryAddress(ulong address)
    {
        return (long)(address & 0x7FFF_FFFF);
    }
}