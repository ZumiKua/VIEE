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
    private const int LinePerChoiceAddressFukyuuban = 0x8b1df;
    private const int LinePerChoiceAddress = 0x8b787;
    private const int ChoicesTextAddressFukyuuban = 0x1b7ef0;
    private const int ChoicesTextAddress = 0x1b8498;
    private const int ChoicesCountAddressFukyuuban = 0x8b21e;
    private const int ChoicesCountAddress = 0x8b7c6;
    private const int ChoicesEnabledAddressFukyuuban = 0xF4171;
    private const int ChoicesEnabledAddress = 0xf4719;
    private const uint ChoiceEnabled = 2;
    private const byte SingleLineChoice = 0x0A;
    private const byte DualLineChoice = 0x14;

        
    private readonly ApiContainer _apiContainer;
    private readonly IExtractResultListener _extractResultListener;
    private readonly bool _fukyuuban;
    private readonly List<byte> _lastBytes = new();
    private readonly StringBuilder _textBuf = new();
    private readonly CharsInfo _info;
    private readonly Dictionary<int, GroupInfoSerialize> _groupInfoDict;
    private bool _choicesEnabled;
    private int _choicesCount;
    private bool _dualLineChoices;
    private readonly List<byte> _lastChoicesBytes = new();
    private readonly List<string> _choices = new();

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
        var gi = CheckTextChange(fastForward);
        CheckChoicesChange(fastForward, gi);
    }

    private void CheckChoicesChange(bool fastForward, GroupInfoSerialize? gi)
    {
        var choicesTextAddress = _fukyuuban ? ChoicesTextAddressFukyuuban : ChoicesTextAddress;
        var choicesEnabledAddress = _fukyuuban ? ChoicesEnabledAddressFukyuuban : ChoicesEnabledAddress;
        var choicesCountAddress = _fukyuuban ? ChoicesCountAddressFukyuuban : ChoicesCountAddress;
        var linePerChoiceAddress = _fukyuuban ? LinePerChoiceAddressFukyuuban : LinePerChoiceAddress;
        var choicesEnabled = _apiContainer.Memory.ReadByte(choicesEnabledAddress) == ChoiceEnabled;
        var choicesCount = (int)_apiContainer.Memory.ReadByte(choicesCountAddress);
        var linePerChoiceFlag = _apiContainer.Memory.ReadByte(linePerChoiceAddress);
        if (linePerChoiceFlag is SingleLineChoice or DualLineChoice)
        {
            _dualLineChoices = linePerChoiceFlag == DualLineChoice;
        }
        var choicesTextMem = choicesEnabled
            ? _apiContainer.Memory.ReadByteRange(choicesTextAddress, choicesCount * 72)
            : Array.Empty<byte>();
        if (_choicesEnabled == choicesEnabled && _choicesCount == choicesCount &&
            choicesTextMem.SequenceEqual(_lastChoicesBytes))
        {
            return;
        }
        
        _lastChoicesBytes.Clear();
        _lastChoicesBytes.AddRange(choicesTextMem);
        _choicesCount = choicesCount;
        _choicesEnabled = choicesEnabled;
        if (!_choicesEnabled || _choicesCount == 0 || _lastChoicesBytes.All(b => b is 0xFF or 0x00) || fastForward)
        {
            _extractResultListener.OnNewChoices(Array.Empty<string>(), -1);
            return;
        }
        
        var hash = CalculateVRAMHash();
        if (!_groupInfoDict.TryGetValue(hash, out gi))
        {
            Console.WriteLine("Unknown Font Page");
            _extractResultListener.OnNewChoices(Array.Empty<string>(), -1);
            return;
        }
        _choices.Clear();
        var linePerChoice = _dualLineChoices ? 2 : 1;
        for (var i = 0; i < choicesCount; i++)
        {
            _textBuf.Clear();
            for (var j = 0; j < linePerChoice; j++)
            {
                var lineIndex = i * linePerChoice + j;
                ParseOneLine(_lastChoicesBytes, lineIndex * 36, 8, gi, false);
            }
            if (_textBuf.Length != 0)
            {
                _choices.Add(_textBuf.ToString());
            }
        }
        _extractResultListener.OnNewChoices(_choices.ToArray(), -1);
    }

    private GroupInfoSerialize? CheckTextChange(bool fastForward)
    {
        var textAddress = _fukyuuban ? TextAddressFukyuuban : TextAddress;
        var textMem = _apiContainer.Memory.ReadByteRange(textAddress, 36 * 3);
        if (_lastBytes.SequenceEqual(textMem))
        {
            return null;
        }

        _lastBytes.Clear();
        _lastBytes.AddRange(textMem);
        if (fastForward || _lastBytes.All(b => b is 0xFF or 0x00))
        {
            _extractResultListener.OnNewText("");
            return null;
        }

        var hash = CalculateVRAMHash();
        if (!_groupInfoDict.TryGetValue(hash, out var gi))
        {
            Console.WriteLine("Unknown Font Page");
            _extractResultListener.OnNewText("");
            return null;
        }

        _textBuf.Clear();
        for (var i = 0; i < 3; i++)
        {
            ParseOneLine(_lastBytes, i * 36, 18, gi, false);
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
        return gi;
    }

    private void ParseOneLine(IReadOnlyList<byte> bytes, int start, int length, GroupInfoSerialize gi, bool zeroAsSpace)
    {
        for (var j = 0; j < length * 2; j += 2)
        {
            int a = bytes[start + j];
            a += bytes[start + j + 1] << 8;
            if (a == 0xFFFF || (zeroAsSpace && a == 0))
            {
                _textBuf.Append("\u3000");
                continue;
            }

            string ch;
            ch = GetChar(a, gi);

            _textBuf.Append(ch);
        }
        //Removing trailing space characters
        for (var j = _textBuf.Length - 1; j >= 0; j--)
        {
            if (_textBuf[j] == '\u3000')
            {
                _textBuf.Length = j;
                continue;
            }
            break;
        }
    }

    private string GetChar(int a, GroupInfoSerialize gi)
    {
        string ch;
        if (a < gi.Chars.Length)
        {
            if (!_info.Chars.TryGetValue(gi.Chars[a], out ch))
            {
                Console.Error.WriteLine($"Illegal Chars Index In Group {gi.Identifier}: {gi.Chars[a]}");
                ch = "？";
            }
        }
        else
        {
            Console.Error.WriteLine($"Illegal Char Index In Game Memory {gi.Identifier}: {a}");
            ch = "\u3000";
        }

        return ch;
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