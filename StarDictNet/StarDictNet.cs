using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StarDictNet
{
    public class StarDictNet
    {
        public int MAX_REGEX_RESULTS = 200;
        private bool HasSyn = false;
        private string BaseName;
        public Ifo Metadata;
        private List<Idx> Idx;
        private List<Syn> Syn;
        private Dictionary<int, HashSet<string>> SynIndexWordGroup;
        private DictZip DictZip;
        private bool IsDictzip = false;
        private Stream DictStream;

        public StarDictNet(string ifoPath)
        {
            try
            {
                var fullPathWoFile = Path.GetDirectoryName(ifoPath);
                BaseName = Path.Join( fullPathWoFile, Path.GetFileNameWithoutExtension(ifoPath) );

                Metadata = new Ifo(ifoPath);
                Idx = new IdxReader(BaseName, (Metadata.IdxOffsetBits == 64), Metadata.IdxFileSize).IdxData;
                if (File.Exists(BaseName + ".syn.dz") || File.Exists(BaseName + ".syn"))
                {
                    Syn = new SynReader(BaseName).SynData;
                    HasSyn = true;
                    SynIndexWordGroup = Syn.GroupBy(k => (int)k.OriginalWordIndex).ToDictionary(g => g.Key, g => g.Select(s => s.SynWord).ToHashSet());
                }
                setupDict();
            }
            catch (System.Exception)
            {
                throw;
            }
        }

        public StarDictNet(Stream ifoStream, Stream idxStream, Stream synStream, Stream dictStream, CompressionOptions compressionOptions)
        {
            Metadata = new Ifo(ifoStream);
            Idx = new IdxReader(idxStream, (Metadata.IdxOffsetBits == 64), Metadata.IdxFileSize, compressionOptions.IDX).IdxData;
            Syn = new SynReader(synStream, compressionOptions.SYN).SynData;
            HasSyn = true;
            SynIndexWordGroup = Syn.GroupBy(k => (int)k.OriginalWordIndex).ToDictionary(g => g.Key, g => g.Select(s => s.SynWord).ToHashSet());
            if (compressionOptions.DICT)
            {
                DictZip = new DictZip(dictStream);
                IsDictzip = true;
            }
            else
            {
                IsDictzip = false;
                DictStream = dictStream;
            }
        }

        public StarDictNet(Stream ifoStream, Stream idxStream, Stream dictStream, CompressionOptions compressionOptions)
        {
            Metadata = new Ifo(ifoStream);
            Idx = new IdxReader(idxStream, (Metadata.IdxOffsetBits == 64), Metadata.IdxFileSize, compressionOptions.IDX).IdxData;
            if (compressionOptions.DICT)
            {
                DictZip = new DictZip(dictStream);
                IsDictzip = true;
            }
            else
            {
                IsDictzip = false;
                DictStream = dictStream;
            }
        }

        ~StarDictNet()
        {
            if (DictStream != null)
            {
                DictStream.Dispose();
            }
        }

        private void setupDict()
        {
            try
            {
                if (File.Exists(BaseName + ".dict.dz"))
                {
                    DictZip = new DictZip(BaseName + ".dict.dz");
                    IsDictzip = true;
                }
                else if(File.Exists(BaseName + ".dict"))
                {
                    IsDictzip = false;
                    DictStream = File.Open((BaseName + ".dict"), FileMode.Open);
                }
                else
                {
                    throw new FileNotFoundException();
                }
            }
            catch (System.Exception)
            {
                throw;
            }
        }

        private string readDict(int offset, int size)
        {
            if(IsDictzip)
            {
                return DictZip.readAt(offset, size);
            }

            else
            {
                DictStream.Seek(offset, SeekOrigin.Begin);
                byte[] defBuf = new byte[size];
                DictStream.Read(defBuf, 0, size);
                var def = Encoding.UTF8.GetString(defBuf);
                return def;
            }
        }

        public HashSet<Tuple<string, string>> GetDef(string word, bool ignoreCase = false, bool ignoreDiacritics = false)
        {
            HashSet<Idx> Idxs = new();

            int maxIdxTry = 0;
            bool isFirstIdxFound = false;

            CompareOptions compOptions = CompareOptions.None;
            if (ignoreCase || ignoreDiacritics)
            {
                if (ignoreCase) { compOptions |= CompareOptions.IgnoreCase; }
                if (ignoreDiacritics) { compOptions |= CompareOptions.IgnoreNonSpace; }
            }

            foreach (var item in Idx)
            {
                if (maxIdxTry == 10) break;
                if (String.Compare(item.Word, word, CultureInfo.CurrentCulture, compOptions) == 0)
                {
                    Idxs.Add(item);
                    if (maxIdxTry == 0) { isFirstIdxFound = true ; maxIdxTry += 1; }
                }

                if (isFirstIdxFound)
                {
                    maxIdxTry += 1;
                }
            }

            if (HasSyn)
            {
                int maxSynTry = 0;
                bool isFirstSynFound = false;
                foreach (var s in Syn)
                {
                    if (maxSynTry == 20) break;
                    if (String.Compare(s.SynWord, word, CultureInfo.CurrentCulture, compOptions) == 0)
                    {
                        Idxs.Add(Idx[(int)s.OriginalWordIndex]);
                        if (maxSynTry == 0) { isFirstSynFound = true ; maxSynTry += 1; }
                    }

                    if (isFirstSynFound)
                    {
                        maxSynTry += 1;
                    }
                }
            }

            HashSet<Tuple<string, string>> res = new();
            foreach (var item in Idxs)
            {
                var def = readDict((int)item.WordDataOffset, (int)item.WordDataSize);
                Tuple<string, string> wordDef = new(item.Word, def);
                res.Add(wordDef);
            }
            return res;
        }

        public HashSet<Tuple<string, string>>
        GetDefRegex(string pattern, bool ignoreCase = true, bool ignoreDiacritics = false, bool matchSyns = false)
        {
            HashSet<Idx> Idxs = new();
            Regex regex;
            TimeSpan abort = new(0,0,0,0,200); // millisec

            var options = RegexOptions.Compiled;
            if (ignoreCase)
            {
                options |= RegexOptions.IgnoreCase;
            }

            try
            {
                regex = new Regex(pattern, options, abort);
            }
            catch (RegexParseException)
            {
                Console.WriteLine("Pattern contains errors!");
                throw;
            }

            foreach (var item in Idx)
            {
                if (Idxs.Count == MAX_REGEX_RESULTS)
                {
                    break;
                }
                string word = item.Word;
                if (ignoreDiacritics) { word = word.RemoveDiacritics(); }
                if (regex.IsMatch(word))
                {
                    Idxs.Add(item);
                }
            }

            if (matchSyns && HasSyn)
            {
                List<int> synIndexes = new();
                foreach (var s in Syn)
                {
                    if (Idxs.Count == MAX_REGEX_RESULTS)
                    {
                        break;
                    }
                    string word = s.SynWord;
                    if (ignoreDiacritics) { word = word.RemoveDiacritics(); }
                    if (regex.IsMatch(word))
                    {
                        synIndexes.Add((int)s.OriginalWordIndex);
                    }
                }

                foreach (var idxOfSyn in synIndexes)
                {
                    Idxs.Add(Idx[idxOfSyn]);
                }
            }

            HashSet<Tuple<string, string>> res = new();
            foreach (var item in Idxs)
            {
                var def = readDict((int)item.WordDataOffset, (int)item.WordDataSize);
                Tuple<string, string> wordDef = new(item.Word, def);
                res.Add(wordDef);
            }
            return res;
        }

        public IEnumerable<string> AllWords()
        {
            return Idx.Select(i => i.Word);
        }

        public HashSet<string> SynsOfWord(string word)
        {
            HashSet<string> syns = new();
            if (HasSyn)
            {
                int index = Idx.FindIndex(i => i.Word == word);
                if (index >= 0)
                {
                    SynIndexWordGroup.TryGetValue(index, out syns);
                }
            }
            return syns;
        }
        public IEnumerable<Entry> AllEntries()
        {
            for (int i = 0; i < Idx.Count; i++)
            {
                Entry entry = new();
                HashSet<string> syns = new();
                if (HasSyn)
                {
                    SynIndexWordGroup.TryGetValue(i, out syns);
                }
                entry.Word = Idx[i].Word;
                entry.Alternatives = syns;
                entry.Definition = readDict((int)Idx[i].WordDataOffset, (int)Idx[i].WordDataSize);

                yield return entry;
            }
        }

        public void SerializeToJson()
        {
            string fileName = BaseName + ".json";
            Console.WriteLine(fileName);
            using FileStream createStream = File.Create(fileName);
            var options = new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = true,
            };
            using Utf8JsonWriter writer = new Utf8JsonWriter(createStream, options);
            // https://www.newtonsoft.com/json/help/html/Performance.htm#ManuallySerialize
            writer.WriteStartArray();
            foreach (var it in AllEntries())
            {
                writer.WriteStartObject();
                writer.WritePropertyName(nameof(it.Word));
                writer.WriteStringValue(it.Word);
                writer.WritePropertyName(nameof(it.Alternatives));
                writer.WriteStartArray();
                if (it.Alternatives != null && it.Alternatives.Count > 0)
                {
                    foreach (var a in it.Alternatives)
                    {
                        writer.WriteStringValue(a);
                    }
                }
                writer.WriteEndArray();
                writer.WritePropertyName(nameof(it.Definition));
                writer.WriteStringValue(it.Definition);
                writer.WriteEndObject();
                writer.Flush();
            }
            writer.WriteEndArray();

            writer.Dispose();
            createStream.Dispose();
        }
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

        private static byte[] ToUint32BigEndian(int number)
        {
            byte[] buf = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)number);
            return buf;
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

        private static void CloseDispose(MemoryStream ms)
        {
            ms.Close();
            ms.Dispose();
        }

        // https://github.com/huzheng001/stardict-3/blob/master/dict/doc/StarDictFileFormat
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
            int idxSize = 0;
            int totalSynsWritten = 0;
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
                // syns
                if (entry.Alternatives != null && entry.Alternatives.Count > 0)
                {
                    foreach (var syn in entry.Alternatives)
                    {
                        synStream.Write(utf8NoBom.GetBytes(syn));
                        synStream.WriteByte(0x00);
                        synStream.Write(ToUint32BigEndian(entry.idx));
                        totalSynsWritten += 1;
                    }
                }
            }
            // ifo
            ifoStream.Write(utf8NoBom.GetBytes("StarDict's dict ifo file\n"));
            ifoStream.Write(utf8NoBom.GetBytes("version=3.0.0\n"));
            ifoStream.Write(utf8NoBom.GetBytes($"bookname={title}\n"));
            ifoStream.Write(utf8NoBom.GetBytes($"wordcount={prepedEntries.Count}\n"));
            if (totalSynsWritten > 0)
                ifoStream.Write(utf8NoBom.GetBytes($"synwordcount={totalSynsWritten}\n"));
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
                if (totalSynsWritten > 0)
                    AddBinToZip(synName, synStream, zipArchiveStream);
                AddBinToZip(ifoName, ifoStream, zipArchiveStream);
            }

            CloseDispose(ifoStream);
            CloseDispose(idxStream);
            CloseDispose(dictStream);
            CloseDispose(synStream);

            return zipMs;
        }
    }
}