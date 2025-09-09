using System.IO;
using System.Text;

namespace VieeSubtitleGenerator;

public class SrtWriter
{

    private List<Entry> _entries = new();

    public void AddEntry(string text, string speaker, long timestamp)
    {
        _entries.Add(new Entry(text, speaker, timestamp));
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

        private bool IsSame(Entry? prev, Entry current, bool keepTypeWriterEffect, bool keepSpeaker)
        {
            if (prev == null)
            {
                return false;
            }
            if (string.IsNullOrEmpty(prev.Value.Text) || string.IsNullOrEmpty(current.Text))
            {
                return false;
            }

            if (prev.Value.Speaker != current.Speaker && keepSpeaker)
            {
                return false;
            }
            if (keepTypeWriterEffect)
            {
                return current.Text == prev.Value.Text;
            }
            return current.Text.StartsWith(prev.Value.Text);
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

        public void WriteTo(Stream stream, long endDuration, bool keepTypeWriterEffect, bool keepSpeaker)
        {
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            var currentStartTime = -1L;
            Entry? currentEntry = null;
            var srtIndex = 0;
            for (var i = 0; i <= _entries.Count; i++)
            {
                var entry = i < _entries.Count ? _entries[i] : new Entry("", "", endDuration > 0 ? endDuration : _entries[^1].Timestamp + 1000L);
                if (IsSame(currentEntry, entry, keepTypeWriterEffect, keepSpeaker))
                {
                    currentEntry = entry;
                }
                else
                {
                    if (currentStartTime >= 0 && currentEntry.HasValue && !string.IsNullOrEmpty(currentEntry.Value.Text))
                    {
                        writer.WriteLine(srtIndex);
                        writer.WriteLine($"{TimestampToSrtFormat(currentStartTime)} --> {TimestampToSrtFormat(entry.Timestamp)}");
                        if (!string.IsNullOrEmpty(currentEntry.Value.Speaker) && keepSpeaker)
                        {
                            writer.Write(currentEntry.Value.Speaker);
                            writer.Write(": ");
                        }
                        writer.WriteLine(currentEntry.Value.Text);
                        writer.WriteLine();
                        srtIndex++;
                    }

                    currentStartTime = entry.Timestamp;
                    currentEntry = entry;
                }
            }
        }
    }

    public struct Entry
    {
        public readonly string Text;
        public readonly string Speaker;
        public readonly long Timestamp;

        public Entry(string text, string speaker, long timestamp)
        {
            Text = text;
            Speaker = speaker;
            Timestamp = timestamp;
        }
    }
}