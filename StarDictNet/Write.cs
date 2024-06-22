using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace StarDictNet;

public partial class StarDictNet
{
    // Half-assed but at least it produces the correct results.
    private static int StarDictComp(string s1, string s2, UTF8Encoding utf8)
    {
        List<byte> s1Bytes = new();
        List<byte> s2Bytes = new();

        foreach (var c in s1)
        {
            if (char.IsAscii(c))
                if (char.IsUpper(c))
                    s1Bytes.Add((byte)(c + 32));
                else
                    s1Bytes.Add((byte)c);
            else
                s1Bytes.AddRange(utf8.GetBytes(c.ToString()));
        }
        foreach (var c in s2)
        {
            if (char.IsAscii(c))
                if (char.IsUpper(c))
                    s2Bytes.Add((byte)(c + 32));
                else
                    s2Bytes.Add((byte)c);
            else
                s2Bytes.AddRange(utf8.GetBytes(c.ToString()));
        }
        var min = Math.Min(s1Bytes.Count, s2Bytes.Count);
        for (int i = 0; i < min; i++)
        {
            var c1 = s1Bytes[i];
            var c2 = s2Bytes[i];
            if (c1 != c2)
                return c1 - c2;
        }
        if (s1.Length != s2.Length)
            return s1.Length - s2.Length;
        else
            return string.Compare(s1, s2, StringComparison.Ordinal);
    }

    private async static Task<List<OutputEntry>> PrepareOutputAsync(List<OutputEntry> entries)
    {
        var utf8NoBom = new UTF8Encoding(false);
        entries.Sort((a,b) => { return StarDictComp(a.Headword, b.Headword, utf8NoBom);});
        int idx = 0;
        int offset = 0;
        foreach (var entry in entries)
        {
            if (idx % 8000 == 0)
                await Task.Delay(1);
            entry.defOffset = offset;
            entry.idx = idx;

            idx += 1;
            offset += entry.DefinitionSize();
        }
        return entries;
    }

    private static List<OutputEntry> PrepareOutput(List<OutputEntry> entries)
    {
        var utf8NoBom = new UTF8Encoding(false);
        entries.Sort((a,b) => { return StarDictComp(a.Headword, b.Headword, utf8NoBom);});
        int idx = 0;
        int offset = 0;
        foreach (var entry in entries)
        {
            entry.defOffset = offset;
            entry.idx = idx;

            idx += 1;
            offset += entry.DefinitionSize();
        }
        return entries;
    }

    private async static Task<List<Syn>> PrepareSynsAsync(List<OutputEntry> entries)
    {
        List<Syn> result = new();
        var utf8NoBom = new UTF8Encoding(false);
        int cnt = 0;
        foreach (var entry in entries)
        {
            if (cnt % 8000 == 0)
                await Task.Delay(1);
            if (entry.Alternatives != null && entry.Alternatives.Count > 0)
            {
                foreach (var syn in entry.Alternatives)
                {
                    result.Add( new Syn(syn, (uint)entry.idx) );
                    cnt++;
                }
            }
        }
        if (result.Count > 1)
            result.Sort((a,b) => { return StarDictComp(a.SynWord, b.SynWord, utf8NoBom);});
        return result;
    }

    private static List<Syn> PrepareSyns(List<OutputEntry> entries)
    {
        List<Syn> result = new();
        var utf8NoBom = new UTF8Encoding(false);
        int cnt = 0;
        foreach (var entry in entries)
        {
            if (entry.Alternatives != null && entry.Alternatives.Count > 0)
            {
                foreach (var syn in entry.Alternatives)
                {
                    result.Add( new Syn(syn, (uint)entry.idx) );
                    cnt++;
                }
            }
        }
        if (result.Count > 1)
            result.Sort((a,b) => { return StarDictComp(a.SynWord, b.SynWord, utf8NoBom);});
        return result;
    }

    private static byte[] ToUint32BigEndian(int number)
    {
        byte[] buf = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)number);
        return buf;
    }

    private static byte[] ToUint32BigEndian(uint number)
    {
        byte[] buf = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, number);
        return buf;
    }

    private async static Task AddBinToZipAsync(string fileName, MemoryStream inMs, ZipArchive zipArchive)
    {
        var entry = zipArchive.CreateEntry(fileName);
        using (var zipEntryStream = entry.Open())
        {
            inMs.Seek(0, SeekOrigin.Begin);
            await inMs.CopyToAsync(zipEntryStream);
        }
    }

    private static void AddBinToZip(string fileName, MemoryStream inMs, ZipArchive zipArchive)
    {
        var entry = zipArchive.CreateEntry(fileName);
        using (var zipEntryStream = entry.Open())
        {
            inMs.Seek(0, SeekOrigin.Begin);
            inMs.CopyTo(zipEntryStream);
        }
    }

    private async static Task CloseDisposeAsync(MemoryStream ms)
    {
        ms.Close();
        await ms.DisposeAsync();
    }

    private static void CloseDispose(MemoryStream ms)
    {
        ms.Close();
        ms.Dispose();
    }

    // https://github.com/huzheng001/stardict-3/blob/master/dict/doc/StarDictFileFormat
    public async static Task<MemoryStream> WriteAsync(List<OutputEntry> entries, string fileName = "Stardict_Dictionary",
    string title = "Title", string author = "Author", string description = "Desc.")
    {
        string ifoName = $"{fileName}.ifo";
        string idxName = $"{fileName}.idx";
        string dictName = $"{fileName}.dict";
        string synName = $"{fileName}.syn";

        MemoryStream ifoStream = new();
        MemoryStream idxStream = new();
        MemoryStream dictStream = new();
        MemoryStream synStream = new();

        var utf8NoBom = new UTF8Encoding(false);

        var prepedEntries = await PrepareOutputAsync(entries);
        var syns = await PrepareSynsAsync(prepedEntries);
        bool hasSyns = syns.Count > 0;
        int idxSize = 0;
        foreach (var entry in prepedEntries)
        {
            // idx
            await idxStream.WriteAsync(entry.HeadwordUTF8());
            idxStream.WriteByte(0x00);
            await idxStream.WriteAsync(ToUint32BigEndian(entry.defOffset));
            await idxStream.WriteAsync(ToUint32BigEndian(entry.DefinitionSize()));
            idxSize += entry.HeadwordUTF8().Length + 1 + 4 + 4;
            // dict
            await dictStream.WriteAsync(entry.DefinitionUTF8());
        }
        // syns
        if (hasSyns)
        {
            foreach (var syn in syns)
            {
                await synStream.WriteAsync(utf8NoBom.GetBytes(syn.SynWord));
                synStream.WriteByte(0x00);
                await synStream.WriteAsync(ToUint32BigEndian(syn.OriginalWordIndex));
            }
        }
        // ifo
        await ifoStream.WriteAsync(utf8NoBom.GetBytes("StarDict's dict ifo file\n"));
        await ifoStream.WriteAsync(utf8NoBom.GetBytes("version=3.0.0\n"));
        await ifoStream.WriteAsync(utf8NoBom.GetBytes($"bookname={title}\n"));
        await ifoStream.WriteAsync(utf8NoBom.GetBytes($"wordcount={prepedEntries.Count}\n"));
        if (hasSyns)
            await ifoStream.WriteAsync(utf8NoBom.GetBytes($"synwordcount={syns.Count}\n"));
        await ifoStream.WriteAsync(utf8NoBom.GetBytes($"idxfilesize={idxSize}\n"));
        await ifoStream.WriteAsync(utf8NoBom.GetBytes($"author={author}\n"));
        await ifoStream.WriteAsync(utf8NoBom.GetBytes($"description={description}\n"));
        await ifoStream.WriteAsync(utf8NoBom.GetBytes($"sametypesequence=h\n"));

        // zipfile
        MemoryStream zipMs = new MemoryStream();
        using (var zipArchiveStream = new ZipArchive(zipMs, ZipArchiveMode.Create, true))
        {
            await AddBinToZipAsync(idxName, idxStream, zipArchiveStream);
            await AddBinToZipAsync(dictName, dictStream, zipArchiveStream);
            if (hasSyns)
                await AddBinToZipAsync(synName, synStream, zipArchiveStream);
            await AddBinToZipAsync(ifoName, ifoStream, zipArchiveStream);
        }

        await CloseDisposeAsync(ifoStream);
        await CloseDisposeAsync(idxStream);
        await CloseDisposeAsync(dictStream);
        await CloseDisposeAsync(synStream);

        zipMs.Seek(0, SeekOrigin.Begin);

        return zipMs;
    }

    public static MemoryStream Write(List<OutputEntry> entries, string fileName = "Stardict_Dictionary",
    string title = "Title", string author = "Author", string description = "Desc.")
    {
        string ifoName = $"{fileName}.ifo";
        string idxName = $"{fileName}.idx";
        string dictName = $"{fileName}.dict";
        string synName = $"{fileName}.syn";

        MemoryStream ifoStream = new();
        MemoryStream idxStream = new();
        MemoryStream dictStream = new();
        MemoryStream synStream = new();

        var utf8NoBom = new UTF8Encoding(false);

        var prepedEntries = PrepareOutput(entries);
        var syns = PrepareSyns(prepedEntries);
        bool hasSyns = syns.Count > 0;
        int idxSize = 0;
        foreach (var entry in prepedEntries)
        {
            // idx
            idxStream.Write(entry.HeadwordUTF8());
            idxStream.WriteByte(0x00);
            idxStream.Write(ToUint32BigEndian(entry.defOffset));
            idxStream.Write(ToUint32BigEndian(entry.DefinitionSize()));
            idxSize += entry.HeadwordUTF8().Length + 1 + 4 + 4;
            // dict
            dictStream.Write(entry.DefinitionUTF8());
        }
        // syns
        if (hasSyns)
        {
            foreach (var syn in syns)
            {
                synStream.Write(utf8NoBom.GetBytes(syn.SynWord));
                synStream.WriteByte(0x00);
                synStream.Write(ToUint32BigEndian(syn.OriginalWordIndex));
            }
        }
        // ifo
        ifoStream.Write(utf8NoBom.GetBytes("StarDict's dict ifo file\n"));
        ifoStream.Write(utf8NoBom.GetBytes("version=3.0.0\n"));
        ifoStream.Write(utf8NoBom.GetBytes($"bookname={title}\n"));
        ifoStream.Write(utf8NoBom.GetBytes($"wordcount={prepedEntries.Count}\n"));
        if (hasSyns)
            ifoStream.Write(utf8NoBom.GetBytes($"synwordcount={syns.Count}\n"));
        ifoStream.Write(utf8NoBom.GetBytes($"idxfilesize={idxSize}\n"));
        ifoStream.Write(utf8NoBom.GetBytes($"author={author}\n"));
        ifoStream.Write(utf8NoBom.GetBytes($"description={description}\n"));
        ifoStream.Write(utf8NoBom.GetBytes($"sametypesequence=h\n"));

        // zipfile
        MemoryStream zipMs = new MemoryStream();
        using (var zipArchiveStream = new ZipArchive(zipMs, ZipArchiveMode.Create, true))
        {
            AddBinToZip(idxName, idxStream, zipArchiveStream);
            AddBinToZip(dictName, dictStream, zipArchiveStream);
            if (hasSyns)
                AddBinToZip(synName, synStream, zipArchiveStream);
            AddBinToZip(ifoName, ifoStream, zipArchiveStream);
        }

        CloseDispose(ifoStream);
        CloseDispose(idxStream);
        CloseDispose(dictStream);
        CloseDispose(synStream);

        zipMs.Seek(0, SeekOrigin.Begin);

        return zipMs;
    }
}