using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BizHawk.Client.Common;

namespace VieeExtractor.Extractors;

public class SLPS02542 : IExtractor
{
    private static readonly Encoding ShiftJISEncoding = Encoding.GetEncoding("shift_jis");

    private const uint BaseAddress = 0x80097448;
    private const int LineCountOffset = 0x760;
    private const int LineAddressOffset = 0x898;
    private const int MaxRead = 3;
    private const int BytesPerRead = 22;
    //In case of an illegal value at the line count address causes the program to enter an infinite loop.
    private const int MaxValidLineCount = 10;
    private const int MapLocationNameCountValue = 0x2D;


    private readonly ApiContainer _apiContainer;
    private readonly IExtractResultListener _extractResultListener;
    private readonly StringBuilder _textBuf = new();
    private readonly List<long> _lineAddresses = new();
    private readonly List<long> _lastLineAddresses = new();
    private readonly byte[] _lineBuf = new byte[MaxRead * BytesPerRead];
    private bool _fastForwardEmptySent = false;
    private bool _registerNotSupported;

    public SLPS02542(ApiContainer apiContainer, IExtractResultListener extractResultListener)
    {
        _apiContainer = apiContainer;
        _extractResultListener = extractResultListener;
    }

    public void FrameEnd(bool fastForward)
    {
        ulong gpAddress = BaseAddress;
        if (!_registerNotSupported)
        {
            var gpValueNullable = _apiContainer.Emulation.GetRegister("gp");
            if (!gpValueNullable.HasValue)
            {
                _registerNotSupported = true;
            }
            else
            {
                gpAddress = gpValueNullable.Value;
            }
        }
        
        if (gpAddress == 0)
        {
            return;
        }
        var baseAddress = ConvertMemoryAddress(gpAddress);
        var lineNumber = _apiContainer.Memory.ReadByte(baseAddress + LineCountOffset);
        if (lineNumber == MapLocationNameCountValue)
        {
            lineNumber = 1;
        }
        if (lineNumber > MaxValidLineCount)
        {
            return;
        }
        var lineAddressStart = ConvertMemoryAddress(_apiContainer.Memory.ReadU32(baseAddress + LineAddressOffset));
        if (lineAddressStart == 0)
        {
            return;
        }
        _lineAddresses.Clear();
        for (var i = 0; i < lineNumber; i++)
        {
            var address = ConvertMemoryAddress(_apiContainer.Memory.ReadU32(lineAddressStart + i * 4));
            _lineAddresses.Add(address);
        }

        if (_lineAddresses.SequenceEqual(_lastLineAddresses))
        {
            return;
        }
        _lastLineAddresses.Clear();
        _lastLineAddresses.AddRange(_lineAddresses);
        if (fastForward)
        {
            if (!_fastForwardEmptySent)
            {
                _extractResultListener.OnNewData(ExtractorData.CreateText("", ""));
                _fastForwardEmptySent = true;
            }
            return;
        }
        _fastForwardEmptySent = false;
        _textBuf.Clear();
        for (var i = 0; i < lineNumber; i++)
        {
            var address = _lineAddresses[i];
            var index = 0;
            for (var j = 0; j < MaxRead; j++)
            {
                var bytes = _apiContainer.Memory.ReadByteRange(address + j * BytesPerRead, BytesPerRead);
                for (var k = 0; k < bytes.Count; k += 2)
                {
                    if (bytes[k] == 0 && bytes[k + 1] == 0)
                    {
                        goto BREAK_LOOP;
                    }

                    if (bytes[k] == 0x40 && bytes[k + 1] == 0)
                    {
                        k += 4;
                        continue;
                    }

                    _lineBuf[index] = bytes[k];
                    _lineBuf[index + 1] = bytes[k + 1];
                    index += 2;
                }
            }
            BREAK_LOOP:
            
            var str = ShiftJISEncoding.GetString(_lineBuf, 0, index);
            if (str.Length > 0)
            {
                _textBuf.Append(str);
                _textBuf.Append('\n');
            }
        }
        _extractResultListener.OnNewData(ExtractorData.CreateText(_textBuf.ToString(), ""));
    }

    private static long ConvertMemoryAddress(ulong address)
    {
        return (long)(address & 0x7FFF_FFFF);
    }
}