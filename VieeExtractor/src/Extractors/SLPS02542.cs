using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BizHawk.Client.Common;

namespace VieeExtractor.Extractors;

public class SLPS02542 : IExtractor
{
    private static readonly Encoding ShiftJISEncoding = Encoding.GetEncoding("shift_jis");

    private const uint BASE_ADDRESS = 0x80097448;
    private const int LINE_NUMBER_OFFSET = 0x892;
    private const int LINE_ADDRESS_OFFSET = 0x898;
    private const int MAX_READ = 3;
    private const int BYTES_PER_READ = 22;
    //In case of an illegal value at the LINE_NUMBER address causes the program to enter an infinite loop.
    private const int INVALID_LINE_COUNT_MAX = 10;


    private readonly ApiContainer _apiContainer;
    private readonly IExtractResultListener _extractResultListener;
    private readonly StringBuilder _textBuf = new();
    private readonly List<long> _lineAddresses = new();
    private readonly List<long> _lastLineAddresses = new();
    private readonly byte[] _lineBuf = new byte[MAX_READ * BYTES_PER_READ];
    private bool _fastForwardEmptySent = false;
    private bool _registerNotSupported;

    public SLPS02542(ApiContainer apiContainer, IExtractResultListener extractResultListener)
    {
        _apiContainer = apiContainer;
        _extractResultListener = extractResultListener;
    }

    public void FrameEnd(bool fastForward)
    {
        ulong gpAddress = BASE_ADDRESS;
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
        var lineNumber = _apiContainer.Memory.ReadS16(baseAddress + LINE_NUMBER_OFFSET);
        if (lineNumber >= INVALID_LINE_COUNT_MAX)
        {
            return;
        }
        var lineAddressStart = ConvertMemoryAddress(_apiContainer.Memory.ReadU32(baseAddress + LINE_ADDRESS_OFFSET));
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
            for (var j = 0; j < MAX_READ; j++)
            {
                var bytes = _apiContainer.Memory.ReadByteRange(address + j * BYTES_PER_READ, BYTES_PER_READ);
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
            _extractResultListener.OnNewData(ExtractorData.CreateText(_textBuf.ToString(), ""));
        }
    }

    private static long ConvertMemoryAddress(ulong address)
    {
        return (long)(address & 0x7FFF_FFFF);
    }
}