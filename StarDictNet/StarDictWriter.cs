using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace StarDictNet.Core;

public class StarDictWriter
{
    private static readonly UTF8Encoding utf8NoBom = new(false);

    private int StarDictComp(string s1, string s2)
    {
        byte[]? array1 = null;
        byte[]? array2 = null;

        Span<byte> b1 = s1.Length <= 256 ? stackalloc byte[s1.Length * 3] : (array1 = ArrayPool<byte>.Shared.Rent(s1.Length * 3));
        Span<byte> b2 = s2.Length <= 256 ? stackalloc byte[s2.Length * 3] : (array2 = ArrayPool<byte>.Shared.Rent(s2.Length * 3));

        try
        {
            int len1 = FillStarDictBytes(s1, b1);
            int len2 = FillStarDictBytes(s2, b2);

            var slice1 = b1[..len1];
            var slice2 = b2[..len2];

            int min = Math.Min(len1, len2);
            for (int i = 0; i < min; i++)
            {
                if (slice1[i] != slice2[i])
                    return slice1[i] - slice2[i];
            }

            if (len1 != len2)
                return len1 - len2;

            return string.Compare(s1, s2, StringComparison.Ordinal);
        }
        finally
        {
            if (array1 != null) ArrayPool<byte>.Shared.Return(array1);
            if (array2 != null) ArrayPool<byte>.Shared.Return(array2);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FillStarDictBytes(string s, Span<byte> destination)
    {
        int written = 0;
        foreach (char c in s)
        {
            if (char.IsAscii(c))
            {
                destination[written++] = (c >= 'A' && c <= 'Z') ? (byte)(c + 32) : (byte)c;
            }
            else
            {
                ReadOnlySpan<char> charSpan = [c];
                written += utf8NoBom.GetBytes(charSpan, destination[written..]);
            }
        }
        return written;
    }

    private async ValueTask<List<OutputEntry>> PrepareOutputAsync(List<OutputEntry> entries)
    {
        entries.Sort((a, b) => { return StarDictComp(a.Headword!, b.Headword!); });
        int idx = 0;
        int offset = 0;
        foreach (var entry in entries)
        {
            entry.DefinitionOffset = offset;
            entry.Idx = idx;

            idx += 1;
            offset += entry.DefinitionSize;
        }
        return entries;
    }

    private async ValueTask<List<Syn>> PrepareSynsAsync(List<OutputEntry> entries)
    {
        List<Syn> result = new();
        foreach (var entry in entries)
        {
            if (entry.Alternatives != null && entry.Alternatives.Count > 0)
            {
                foreach (var syn in entry.Alternatives)
                {
                    result.Add(new Syn(syn, (uint)entry.Idx));
                }
            }
        }
        if (result.Count > 1)
            result.Sort((a,b) => { return StarDictComp(a.SynWord, b.SynWord);});
        return result;
    }

    private void WriteUint32BigEndian(Stream stream, uint number)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, number);
        stream.Write(buf);
    }

    private void WriteUint32BigEndian(Stream stream, int number) => WriteUint32BigEndian(stream, (uint)number);

    // https://github.com/huzheng001/stardict-3/blob/master/dict/doc/StarDictFileFormat
    public async ValueTask WriteAsync(List<OutputEntry> entries, string folder, string fileName = "Stardict Dictionary",
    string title = "Title", string author = "Author", string description = "Desc.")
    {
        try
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not created the specified directory while writing StarDict dictionary, exception: {ex}");
            return;
        }
        
        using var ifoFs = File.Create(Path.Combine(folder, $"{fileName}.ifo"));
        using var idxFs = File.Create(Path.Combine(folder, $"{fileName}.idx"));
        using var dictFs = File.Create(Path.Combine(folder, $"{fileName}.dict"));

        var preparedEntries = await PrepareOutputAsync(entries);
        var syns = await PrepareSynsAsync(preparedEntries);
        bool hasSyns = syns.Count > 0;
        int idxSize = 0;
        foreach (var entry in preparedEntries)
        {
            // idx
            await idxFs.WriteAsync(entry.HeadwordUTF8);
            idxFs.WriteByte(0x00);
            WriteUint32BigEndian(idxFs, entry.DefinitionOffset);
            WriteUint32BigEndian(idxFs, entry.DefinitionSize);
            idxSize += entry.HeadwordUTF8.Length + 1 + 4 + 4;
            
            // dict
            await dictFs.WriteAsync(entry.DefinitionUTF8);
        }

        // syns
        if (hasSyns)
        {
            using var synFs = File.Create(Path.Combine(folder, $"{fileName}.syn"));
            foreach (var syn in syns)
            {
                await synFs.WriteAsync(utf8NoBom.GetBytes(syn.SynWord));
                synFs.WriteByte(0x00);
                WriteUint32BigEndian(synFs, syn.OriginalWordIndex);
            }
        }
        // ifo
        await ifoFs.WriteAsync(utf8NoBom.GetBytes("StarDict's dict ifo file\n"));
        await ifoFs.WriteAsync(utf8NoBom.GetBytes("version=3.0.0\n"));
        await ifoFs.WriteAsync(utf8NoBom.GetBytes($"bookname={title}\n"));
        await ifoFs.WriteAsync(utf8NoBom.GetBytes($"wordcount={preparedEntries.Count}\n"));
        if (hasSyns)
            await ifoFs.WriteAsync(utf8NoBom.GetBytes($"synwordcount={syns.Count}\n"));
        await ifoFs.WriteAsync(utf8NoBom.GetBytes($"idxfilesize={idxSize}\n"));
        await ifoFs.WriteAsync(utf8NoBom.GetBytes($"author={author}\n"));
        await ifoFs.WriteAsync(utf8NoBom.GetBytes($"description={description}\n"));
        await ifoFs.WriteAsync(utf8NoBom.GetBytes($"sametypesequence=h\n"));
    }
}