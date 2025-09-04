using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BizHawk.Client.Common;

namespace VieeExtractor.Extractors;

public class SLPS03015 : IExtractor
{
    
    private const int TextAddressFukyuuban = 0x1a4e30;
    private const int TextAddress = 0x1a53d8;
    
    private readonly ApiContainer _apiContainer;
    private readonly IExtractResultListener _extractResultListener;
    private readonly bool _fukyuuban;
    private readonly List<byte> _lastBytes = new();
    private readonly StringBuilder _textBuf = new();
    private readonly CharsInfo _info;
    private readonly Dictionary<int, GroupInfoSerialize> _groupInfoDict;

    public SLPS03015(string hash, ApiContainer apiContainer, IExtractResultListener extractResultListener)
    {
        _fukyuuban = hash == "37946519"; 
        _apiContainer = apiContainer;
        _extractResultListener = extractResultListener;
        _info = LoadInfo();
        _groupInfoDict = new Dictionary<int, GroupInfoSerialize>();
        foreach (var gi in _info.GroupInfos)
        {
            _groupInfoDict[gi.Identifier] = gi;
        }
    }

    public void FrameEnd(bool fastForward)
    {
        CheckTextChange();
    }

    private void CheckTextChange()
    {
        var textAddress = _fukyuuban ? TextAddressFukyuuban : TextAddress;
        var textMem = _apiContainer.Memory.ReadByteRange(textAddress, 36 * 3);
        if (_lastBytes.SequenceEqual(textMem))
        {
            return;
        }

        _lastBytes.Clear();
        _lastBytes.AddRange(textMem);
        if (_lastBytes.All(b => b is 0xFF or 0x00))
        {
            _extractResultListener.OnNewText("");
            return;
        }

        var hash = CalculateVRAMHash();
        if (!_groupInfoDict.TryGetValue(hash, out var gi))
        {
            Console.WriteLine("Unknown Font Page");
            _extractResultListener.OnNewText("");
            return;
        }

        _textBuf.Clear();
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 36; j += 2)
            {
                int a = _lastBytes[i * 36 + j];
                a += _lastBytes[i * 36 + j + 1] << 8;
                if (a == 0xFFFF)
                {
                    _textBuf.Append("\u3000");
                    continue;
                }

                if (a < gi.Chars.Length)
                {
                    if (_info.Chars.TryGetValue(gi.Chars[a], out var ch))
                    {
                        _textBuf.Append(ch);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Illegal Chars Index In Group {gi.Identifier}: {gi.Chars[a]}");
                        _textBuf.Append("？");
                    }
                }
                else
                {
                    Console.Error.WriteLine($"Illegal Char Index In Game Memory {gi.Identifier}: {a}");
                    _textBuf.Append("\u3000");
                }
            }
            //Removing trailing space characters
            for (var j = _textBuf.Length - 1; j >= 0; j--)
            {
                if (_textBuf[j] == '\u3000')
                {
                    continue;
                }
                _textBuf.Length = j + 1;
                break;
            }

            _textBuf.Append("\n");
        }

        //Removing trailing newline characters
        for (var i = _textBuf.Length - 1; i >= 0; i--)
        {
            if (_textBuf[i] == '\n')
            {
                if (i == 0)
                {
                    _textBuf.Length = 0;
                }
                continue;
            }
            _textBuf.Length = i + 1;
            break;
        }

        _extractResultListener.OnNewText(_textBuf.ToString());
    }

    private CharsInfo LoadInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var zipPath = Path.Combine(Path.GetDirectoryName(assembly.Location)!, "SLPS03015.zip");
        using var zipFile = ZipFile.OpenRead(zipPath)!;
        var e = zipFile.GetEntry("chars_info.json");
        return JsonSerializer.Deserialize<CharsInfo>(e!.Open())!;
    }
    
    private int CalculateVRAMHash()
    {
        
        const int baseY = 256;
        const int baseX = 1792;
        
        const int lineLen = 7 * 18;
        const int p = 16777619;
        int hash;
        unchecked
        {
            hash = (int)2166136261;
        }
        for (int i = 7; i < 18 * 14; i += 14)
        {
            var start = (baseY + i) * 2048 + baseX;
            var read = _apiContainer.Memory.ReadByteRange(start, lineLen, "GPURAM");
            unchecked
            {
                for (var j = 0; j < lineLen; ++j)
                {
                    hash = (hash ^ read[j]) * p;
                }
            }
        }
        return hash;
    }
    
    public class CharsInfo
    {
        public List<GroupInfoSerialize> GroupInfos { get; set; }
        public Dictionary<int, string> Chars { get; set; }
    }

    public class GroupInfoSerialize
    {
        public int Identifier { get; set; }
        public int[] Chars { get; set; }
        
    }
}