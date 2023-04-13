using System.IO.Compression;
using System.Text;

namespace StarDictNet
{
    public class SynReader
    {
        private byte[]? _syn_array;
        public List<Syn> SynData = new();

        public SynReader(string path)
        {
            if (File.Exists(path + ".syn.dz"))
            {
                using FileStream compressedFileStream = File.Open((path + ".syn.dz"), FileMode.Open);
                readSyn(compressedFileStream, true);
            }
            else if(File.Exists(path + ".syn"))
            {
                using FileStream synFileStream = File.Open((path + ".syn"), FileMode.Open);
                readSyn(synFileStream, false);
            }
            else
            {
                throw new FileNotFoundException();
            }
        }

        public SynReader(Stream stream, bool compressed)
        {
            readSyn(stream, compressed);
        }

        public void readSyn(Stream stream, bool compressed)
        {
            try
            {
                if (compressed)
                {
                    using MemoryStream ms = new MemoryStream();
                    using var decompressor = new GZipStream(stream, CompressionMode.Decompress);
                    decompressor.CopyTo(ms);
                    _syn_array = ms.ToArray();
                }
                else
                {
                    using MemoryStream ms = new MemoryStream();
                    stream.CopyTo(ms);
                    _syn_array = ms.ToArray();
                }
            }
            catch (System.Exception)
            {
                
                throw;
            }

            int pos = 0;
            while (pos < _syn_array.Length)
            {
                int beg = pos;
                uint original_word_index = 0;
                pos = Array.IndexOf(_syn_array, (byte)0, beg);
                if (pos < 0)
                {
                    Console.WriteLine("Corrupt syn file. pos < 0");
                    break;
                }
                var syn_word = Encoding.UTF8.GetString(_syn_array[beg..pos]);
                pos += 1;
                if (pos + 4 > _syn_array.Length)
                {
                    Console.WriteLine("Corrupt syn file. pos + 4 > _syn_array.Length");
                    break;
                }
                original_word_index = BitConverter.ToUInt32(_syn_array[(pos)..(pos+4)].Reverse().ToArray());
                pos += 4;
                
                Syn syn = new(syn_word, original_word_index);
                SynData.Add(syn);
            }
        }
    }
}