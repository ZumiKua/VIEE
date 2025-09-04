using System.IO;
using System.Text;

namespace VieeSubtitleGenerator;

public class SrtWriter
{

    private List<Entry> _entries = new();

    public void AddEntry(string text, long timestamp)
    {
        _entries.Add(new Entry(text, timestamp));
    }

    public void Clear()
    {
        _entries.Clear();
    }

    public Writer GetWriterAndClear()
    {
        var entries = _entries;
        _entries = new List<Entry>();
        return new Writer(entries);
    }

    public class Writer
    {
        private readonly List<Entry> _entries;

        public Writer(List<Entry> entries)
        {
            _entries = entries;
        }

        private bool IsSame(string prev, string current, bool keepTypeWriterEffect)
        {
            if (string.IsNullOrEmpty(prev) || string.IsNullOrEmpty(current))
            {
                return false;
            }
            if (keepTypeWriterEffect)
            {
                return current == prev;
            }
            return current.StartsWith(prev);
        }
        
        public bool HasContent()
        {
            return _entries.Count > 0 && _entries.Any(e => !string.IsNullOrWhiteSpace(e.Text));
        }

        private string TimestampToSrtFormat(long ms)
        {
            var milli = ms % 1000L;
            var sec = ms / 1000 % 60;
            var min = ms / 60000 % 60;
            var hour = ms / 3600000L;
            return $"{hour:00}:{min:00}:{sec:00},{milli:0000}";
        }

        public void WriteTo(Stream stream, long endDuration, bool keepTypeWriterEffect)
        {
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            var currentStartTime = -1L;
            var currentString = "";
            var srtIndex = 0;
            for (var i = 0; i <= _entries.Count; i++)
            {
                var entry = i < _entries.Count ? _entries[i] : new Entry("", endDuration > 0 ? endDuration : _entries[^1].Timestamp + 1000L);
                if (IsSame(currentString, entry.Text, keepTypeWriterEffect))
                {
                    currentString = entry.Text;
                }
                else
                {
                    if (currentStartTime >= 0 && !string.IsNullOrEmpty(currentString))
                    {
                        writer.WriteLine(srtIndex);
                        writer.WriteLine($"{TimestampToSrtFormat(currentStartTime)} --> {TimestampToSrtFormat(entry.Timestamp)}");
                        writer.WriteLine(currentString);
                        writer.WriteLine();
                        srtIndex++;
                    }

                    currentStartTime = entry.Timestamp;
                    currentString = entry.Text;
                }
            }
        }
    }

    public struct Entry
    {
        public readonly string Text;
        public readonly long Timestamp;

        public Entry(string text, long timestamp)
        {
            Text = text;
            Timestamp = timestamp;
        }
    }
}