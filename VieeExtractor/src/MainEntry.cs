using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using BizHawk.Client.EmuHawk;

namespace VieeExtractor;

using BizHawk.Client.Common;

[ExternalTool("VieeExtractor")] // this appears in the Tools > External Tools submenu in EmuHawk
public sealed class MainEntry : ToolFormBase, IExternalToolForm {
    protected override string WindowTitleStatic => "VieeExtractor";
    
    [OptionalApi]
    private IEmulationApi? MaybeEmulationApi { get; set; }

    public ApiContainer? MaybeApiContainer { get; set; }
    
    private ApiContainer APIs => MaybeApiContainer!;

    private List<Byte> _lastBytes = [];
    private StringBuilder _textOut = new();
    private readonly Label _label;
    private readonly CharsInfo _info;
    private readonly Dictionary<int, GroupInfoSerialize> _groupInfoDict;

    public MainEntry() {
        ClientSize = new Size(480, 320);
        SuspendLayout();
        _label = new Label { AutoSize = true, Text = "Hello, world!" };
        Controls.Add(_label);
        ResumeLayout(performLayout: false);
        PerformLayout();
        var assembly = Assembly.GetExecutingAssembly();
        var jsonPath = Path.Combine(Path.GetDirectoryName(assembly.Location)!, "char.json");
        _info = LoadInfo();
        _groupInfoDict = new Dictionary<int, GroupInfoSerialize>();
        foreach (var gi in _info.GroupInfos)
        {
            _groupInfoDict[gi.Identifier] = gi;
        }
    }

    private CharsInfo LoadInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var zipPath = Path.Combine(Path.GetDirectoryName(assembly.Location)!, "chars_info.zip");
        using var zipFile = ZipFile.OpenRead(zipPath)!;
        var e = zipFile.GetEntry("chars_info.json");
        return JsonSerializer.Deserialize<CharsInfo>(e!.Open())!;
    }

    public override void Restart()
    {
        base.Restart();
        var gameInfo = MaybeEmulationApi?.GetGameInfo();
        Console.WriteLine($"Hash: {gameInfo?.Hash} Domains: {string.Join(",", APIs.Memory.GetMemoryDomainList())}");
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
            var read = APIs.Memory.ReadByteRange(start, lineLen, "GPURAM");
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

    protected override void UpdateAfter()
    {
        base.UpdateAfter();
        var textMem = APIs.Memory.ReadByteRange(0x1a4e30, 36 * 3);
        if (_lastBytes.SequenceEqual(textMem))
        {
            return;
        }
        _lastBytes.Clear();
        _lastBytes.AddRange(textMem);
        if (_lastBytes.All(b => b is 0xFF or 0x00))
        {
            Console.WriteLine("DataChanged: Not Displaying text.");
            _label.Text = "Not Displaying Text";
            return;
        }

        var hash = CalculateVRAMHash();
        if (!_groupInfoDict.TryGetValue(hash, out var gi))
        {
            Console.WriteLine("Unknown Font Page");
            _label.Text = "Unknown Font Page";
            return;
        }

        _textOut.Clear();
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 36; j+=2)
            {
                int a = _lastBytes[i * 36 + j];
                a += _lastBytes[i * 36 + j + 1] << 8;
                if (a == 0xFFFF)
                {
                    break;
                }
                
                if (a < gi.Chars.Length)
                {
                    if (_info.Chars.TryGetValue(gi.Chars[a], out var ch))
                    {
                        _textOut.Append(ch);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Illegal Chars Index In Group {gi.Identifier}: {gi.Chars[a]}");
                        _textOut.Append("？");
                    }
                }
                else
                {
                    Console.Error.WriteLine($"Illegal Char Index In Game Memory {gi.Identifier}: {a}");
                    _textOut.Append("\u3000");
                }
            }
            _textOut.Append("\n");
            _label.Text = _textOut.ToString();
        }
        
    }

    protected override void FastUpdateAfter()
    {
        base.FastUpdateAfter();
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
