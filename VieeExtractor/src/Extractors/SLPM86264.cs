using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using BizHawk.Client.Common;

namespace VieeExtractor.Extractors;

public class SLPM86264 : IExtractor
{
    /// <summary>
    /// LogCode为如下代码编译后的结果
    /// # 1. 将 s0 指向的值存入 0x80063560
    /// # 首先需要加载 32 位地址的高 16 位和低 16 位
    ///     lui     $t0, 0x8006          # 加载高位地址到临时寄存器 t0
    ///     lw      $t1, 0($s0)          # 取出 s0 指向的内存值
    ///     sw      $t1, 0x3560($t0)     # 将值存入 0x80063560
    ///
    /// # 2. 调用 0x80023868
    /// # 由于参数已在 a0 中，且要求不修改 ra 并直接返回给当前函数的调用者，
    /// # 我们使用无条件跳转指令 'j' 而不是 'jal'。
    ///     lui     $t2, 0x8002          # 加载目标函数的高位地址
    ///     ori     $t2, $t2, 0x3868     # 组合低位地址
    ///     jr      $t2                  # 跳转到目标地址，执行完毕后它会直接返回给最初的 RA
    /// </summary>
    private static readonly byte[] LogCode =
    [
        0x06, 0x80, 0x08, 0x3c, 0x00, 0x00, 0x09, 0x8e, 0x00, 0x00, 0x00, 0x00, 0x60, 0x35, 0x09, 0xad, 0x02, 0x80,
        0x0a, 0x3c, 0x68, 0x38, 0x4a, 0x35, 0x08, 0x00, 0x40, 0x01, 0x00, 0x00, 0x00, 0x00
    ];

    private const uint LogCodeAddr = 0x80063564;
    private const uint LogCodeAddrDefaultValue = 0x20797261;

    /// <summary>
    /// jal 0x80063564
    /// </summary>
    private static readonly byte[] JumpCode = [0x59, 0x8d, 0x01, 0x0c];
    private const uint JumpCodeAddr = 0x8001de18;
    private const uint JumpCodeAddrDefaultValue = 0x0c008e1a;
    
    private const uint TalkStartPointerAddr = 0x80063560;
    private const uint TasksLinkedListHeadAddress = 0x8006591c;
    
    private const uint TaskStructEntryFunctionOffset = 0x14;
    private const uint TaskStructNextTaskOffset = 0x0c;
    private const uint TaskStructReturnPcOffset = 0x44;
    private const uint TaskStructTextPointerOffset = 0x180;
    
    private const uint TextTaskEntryFunction = 0x8001ddfc;
    private const uint TextTaskTypingReturnPc = 0x8001d4dc;
    
    private const uint PlayerNamePointerAddr = 0x80065510;

    private const int MaxReadLength = 512;

    private readonly ApiContainer _apiContainer;
    private readonly IExtractResultListener _listener;
    private readonly string _glyphs;
    private readonly StringBuilder _textBuf = new();
    private string _lastText = "";
    private uint _rangeStart = 0;
    private uint _rangeEnd = 0;
    private uint _badTextStartAddress = 0;

    public SLPM86264(ApiContainer apiContainer, IExtractResultListener listener)
    {
        _apiContainer = apiContainer;
        _listener = listener;
        _glyphs = LoadInfo();
    }

    private string LoadInfo()
    {
        
        var assembly = Assembly.GetExecutingAssembly();
        var zipPath = Path.Combine(Path.GetDirectoryName(assembly.Location)!, "SLPM86264.zip");
        using var zipFile = ZipFile.OpenRead(zipPath)!;
        var e = zipFile.GetEntry("all_text.txt");
        using var reader = new StreamReader(e!.Open(), Encoding.UTF8);
        var glyphs = reader.ReadToEnd();
        return glyphs.Replace("\n", "").Replace("\r", "");
    }
    
    private static long ConvertMemoryAddress(uint address)
    {
        return address & 0x7FFF_FFFF;
    }

    private void EnsureCode()
    {
        if (_apiContainer.Memory.ReadU32(ConvertMemoryAddress(LogCodeAddr)) == LogCodeAddrDefaultValue)
        {
            Console.WriteLine("Write To Address");
            _apiContainer.Memory.WriteByteRange(ConvertMemoryAddress(LogCodeAddr), LogCode);
        }

        //这里，我们修改代码，其实是需要考虑 i-cache 是否会 cache 原有的 jmp 指令
        //这里我们的运气不错，在 FUN_8001ddfc 执行后，其他代码会占据 i-cache 中这条指令的 cache 位置，导致下次执行到 FUN_8001ddfc 时
        //我们修改后的跳转指令会被重新读入 i-cache 并生效。
        if (_apiContainer.Memory.ReadU32(ConvertMemoryAddress(JumpCodeAddr)) == JumpCodeAddrDefaultValue)
        {
            Console.WriteLine("Write To Jump");
            _apiContainer.Memory.WriteByteRange(ConvertMemoryAddress(JumpCodeAddr), JumpCode);
        }
    }
    
    private uint GetTaskStruct()
    {
        // 地址 8006591c指向任务结构体链表的开头。
        //
        // 任务结构体的结构
        // 0x0 flags 0x8000表示终止待回收，我们需要跳过该任务。（一般来说不应该在链表中看见）
        // 0x08/0x0c prev/next
        // 0x14 入口函数 （如果这里值为0x8001ddfc说明这个任务是显示文字）
        // 0x180 文字相关的内容在这里。
        //

        // param_1 + 0x180（即 puVar3）可视作 TextExecCtx*：
        // +0x00: 脚本流指针
        //     +0x04: 文本执行 flags（见 0x200/0x400/0x700）
        // +0x06/+0x07: 当前绘制坐标 x/y
        //     +0x08/+0x09: 速度配置与当前延时计数
        //     +0x0A: 逐字音效 ID
        //     +0x0B: 暂存旧 DAT_80065811

        var currentTask = _apiContainer.Memory.ReadU32(ConvertMemoryAddress(TasksLinkedListHeadAddress));
        while (currentTask != 0)
        {
            var convertedAddress = (uint)ConvertMemoryAddress(currentTask);
            var flags = _apiContainer.Memory.ReadU16(convertedAddress);
            if ((flags & 0x8000) == 0)
            {
                var entryFunction = _apiContainer.Memory.ReadU32(convertedAddress + TaskStructEntryFunctionOffset);
                if (entryFunction == TextTaskEntryFunction)
                {
                    return currentTask;
                }
            }

            currentTask = _apiContainer.Memory.ReadU32(convertedAddress + TaskStructNextTaskOffset);
        }

        return 0;
    }

    private void DispatchText(string currentText, bool fastForward = false)
    {
        if (currentText != _lastText)
        {
            _lastText = currentText;
            if (!fastForward || string.IsNullOrEmpty(currentText))
            {
                _listener.OnNewData(ExtractorData.CreateText(currentText, ""));
            }
        }
    }

    public void FrameEnd(bool fastForward)
    {
        EnsureCode();
        var taskStructPointer = GetTaskStruct();
        if (taskStructPointer == 0)
        {
            _rangeStart = 0;
            _rangeEnd = 0;
            DispatchText("", fastForward);
            return;
        }
        var startAddress = _apiContainer.Memory.ReadU32(ConvertMemoryAddress(TalkStartPointerAddr));
        if (startAddress is < 0x80000000u or >= 0x80200000u || startAddress == _badTextStartAddress)
        {
            //Console.WriteLine($"Bad Address {startAddress:X8}");
            _rangeStart = 0;
            _rangeEnd = 0;
            DispatchText("", fastForward);
            return;
        }
        var typing = IsTyping(taskStructPointer);
        var currentTextPointer = GetCurrentTextPointer(taskStructPointer);
        if (!typing)
        {
            //如果当前没处于打字状态，则Pointer会指向当前显示的内容的结尾（0x0或者0xc）的下一个字节，这里我们减一来让它回到当前显示的内容的范围内。
            currentTextPointer -= 1;
        }

        if (currentTextPointer >= _rangeStart && currentTextPointer <= _rangeEnd)
        {
            //当前显示的内容仍处于我们上次的解析范围内，可以直接退出处理
            return;
        }

        foreach (var (s, e, str) in ExtractText(startAddress))
        {
            Console.WriteLine($"{s:x8} {e:x8} {str}");
            if (currentTextPointer >= s && currentTextPointer <= e)
            {
                _rangeStart = s;
                _rangeEnd = e;
                DispatchText(str, fastForward);
                return;
            }
        }
        
        //走到这里，说明我们没有找到范围和 currentTextPointer 重叠的字符串。可能原因是我们的 MaxReadLength 太小，遇到了0x1F指令，
        //此时我们也无法确定一个合理的 _rangeStart 到 _rangeEnd，我们直接标记当前的 startAddress 无法解析，然后 Dispatch 空字符串。
        _badTextStartAddress = startAddress;
        DispatchText("", fastForward);
        
    }

    private IEnumerable<(uint, uint, string)> ExtractText(uint startAddress)
    {
        var range = _apiContainer.Memory.ReadByteRange(ConvertMemoryAddress(startAddress), MaxReadLength);
        if (range == null) yield break;
        var currentStart = startAddress;
        _textBuf.Clear();
        for (var i = 0; i < range.Count; i++)
        {
            var ch = range[i];
            switch (ch)
            {
                case 0 or 0xc:
                    var s = _textBuf.ToString();
                    _textBuf.Clear();
                    var ret = (currentStart, (uint)(startAddress + i), s);
                    currentStart = (uint)(startAddress + i + 1);
                    yield return ret;
                    if (ch == 0)
                    {
                        yield break;
                    }
                    break;
                case 0xA:
                    _textBuf.AppendLine();
                    break;
                case >= 0x20 or <= 0x4:
                    AppendChar(range, ref i, _textBuf);
                    break;
                case 0x1F:
                    //我们无法正确识别0x1F的参数长度，这里直接 yield break
                    yield break;
                case 0xF:
                    AppendPlayerName(_textBuf);
                    break;
                default:
                    if (!ParseCommands(range, ref i))
                    {
                        yield break;
                    }
                    break;
            }
        }
        //走到这里说明我们没找到 \0 ，直接返回
        yield break;
    }

    private void AppendChar(IReadOnlyList<byte> buffer, ref int i, StringBuilder textBuf)
    {
        var ch = buffer[i];
        int sh = ch;
        if (ch <= 0x4)
        {
            i++;
            sh = buffer[i];
            sh += ch << 8;
        }
        var index = (sh - (sh >> 8)) - 0x20;
        if (index >= 0 && index < _glyphs.Length)
        {
            textBuf.Append(_glyphs[index]);
        }

    }

    private void AppendPlayerName(StringBuilder textBuf)
    {
        Console.WriteLine("Append Player Name");
        var name = _apiContainer.Memory.ReadByteRange(ConvertMemoryAddress(PlayerNamePointerAddr), 0x10);
        //打印 name 的内容
        Console.WriteLine(string.Join(" ", name.Select(i => $"{i:X2}")));
        for (int i = 0; i < name.Count; i++)
        {
            if (name[i] == 0)
            {
                break;
            }
            AppendChar(name, ref i, textBuf);
        }
        
    }

    private bool IsTyping(uint taskStruct)
    {
        var taskReturnPc = _apiContainer.Memory.ReadU32(ConvertMemoryAddress(taskStruct + TaskStructReturnPcOffset));
        return taskReturnPc == TextTaskTypingReturnPc;
    }
    
    private uint GetCurrentTextPointer(uint taskStruct)
    {
        var taskStructPointer = _apiContainer.Memory.ReadU32(ConvertMemoryAddress(taskStruct + TaskStructTextPointerOffset));
        return taskStructPointer;
    }
    
    private bool ParseCommands(IReadOnlyList<byte> buffer, ref int offset)
    {
        if (offset < 0 || offset >= buffer.Count) return false;

        var token = buffer[offset];
        var cur = offset;                // 用临时游标，成功后再提交

        var ok = token switch
        {
            // 1-byte 参数
            0x09 or 0x0E or 0x11 or 0x12 or 0x13 or 0x14
                => TryAdvance(buffer, ref cur, 1),

            // 无参数
            0x05 or 0x06 or 0x07 or 0x08 or 0x0A or 0x0B or 0x0C or 0x0D or 0x0F or 0x10 or 0x17
                => true,

            // 1 or 2-byte 参数（首字节最高位决定）
            0x15 or 0x16
                => TrySkipVarIndex(buffer, ref cur),

            // 条件参数 + 4字节偏移
            0x18 or 0x19
                => TrySkipConditionExpr(buffer, ref cur) && TryAdvance(buffer, ref cur, 4),

            // 4-byte 参数
            0x1A
                => TryAdvance(buffer, ref cur, 4),

            // 1字节 + C字符串(\0结尾)
            0x1B
                => TryAdvance(buffer, ref cur, 1) && TrySkipCString(buffer, ref cur),

            // 3-byte 参数
            0x1C or 0x1D
                => TryAdvance(buffer, ref cur, 3),

            // 2-byte 参数
            0x1E
                => TryAdvance(buffer, ref cur, 2),

            // 未知 token：中断
            _ => false
        };

        if (!ok) return false;

        offset = cur; // 仅成功时提交
        return true;
    }

    private static bool TrySkipVarIndex(IReadOnlyList<byte> buffer, ref int cur)
    {
        // FUN_8001d2fc: 先读1字节，若最高位=1再读1字节
        if (!TryAdvance(buffer, ref cur, 1)) return false;
        var b0 = buffer[cur];
        if ((b0 & 0x80) != 0)
            return TryAdvance(buffer, ref cur, 1);
        return true;
    }

    private static bool TrySkipConditionExpr(IReadOnlyList<byte> buffer, ref int cur)
    {
        // FUN_8001d33c:
        // mode=0/1/2 -> 变长索引(1或2)
        // mode=3/4   -> 1字节
        if (!TryAdvance(buffer, ref cur, 1)) return false;
        byte mode = buffer[cur];

        return mode switch
        {
            0 or 1 or 2 => TrySkipVarIndex(buffer, ref cur),
            3 or 4      => TryAdvance(buffer, ref cur, 1),
            _           => false
        };
    }

    private static bool TrySkipCString(IReadOnlyList<byte> buffer, ref int cur)
    {
        while (cur < buffer.Count - 1)
        {
            if (buffer[++cur] == 0x00)
                return true;
        }
        return false; // 没找到 '\0'，视为长度不足/数据不完整
    }

    private static bool TryAdvance(IReadOnlyList<byte> buffer, ref int cur, int count)
    {
        if (count < 0) return false;
        if (cur < 0 || cur + count > buffer.Count) return false;
        cur += count;
        return true;
    }
}