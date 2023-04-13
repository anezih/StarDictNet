using System.IO.Compression;
using System.Text;

namespace StarDictNet
{
    public class IdxReader
    {
        private bool _is_64bit;
        private byte[]? _idx_array;

        public List<Idx> IdxData = new();

        public IdxReader(string path, bool is_64bit, int idxSize)
        {
           if (File.Exists(path + ".idx.dz"))
           {
                using FileStream compressedFileStream = File.Open((path + ".idx.dz"), FileMode.Open);
                readIdx(compressedFileStream, true, idxSize, is_64bit);
           }

           else if(File.Exists(path + ".idx"))
           {
                using FileStream idxFileStream = File.Open((path + ".idx"), FileMode.Open);
                readIdx(idxFileStream, false, idxSize, is_64bit);
           }
           else
           {
            throw new FileNotFoundException();
           }
        }

        public IdxReader(Stream stream, bool is_64bit, int idxSize, bool compressed)
        {
            readIdx(stream, compressed, idxSize, is_64bit);
        }

        public void readIdx(Stream stream, bool compressed, int idxSize, bool is_64bit)
        {
            try
            {
                if (compressed)
                {
                    using MemoryStream ms = new MemoryStream();
                    using var decompressor = new GZipStream(stream, CompressionMode.Decompress);
                    decompressor.CopyTo(ms);
                    _idx_array = ms.ToArray();
                }
                else
                {
                    using MemoryStream ms = new MemoryStream();
                    stream.CopyTo(ms);
                    _idx_array = ms.ToArray();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            int pos = 0;
            while (pos < idxSize)
            {
                int beg = pos;
                ulong word_data_offset = 0;
                uint word_data_size = 0;
                pos = Array.IndexOf(_idx_array, (byte)0, beg);
                if (pos < 0)
                {
                    Console.WriteLine("Corrupt idx file. pos < 0");
                    break;
                }
                var word = Encoding.UTF8.GetString(_idx_array[beg..pos]);
                pos += 1;
                if (pos + 8 > idxSize)
                {
                    Console.WriteLine("Corrupt idx file. pos + 8 > idxSize");
                    break;
                }
                if (is_64bit)
                {
                    word_data_offset = BitConverter.ToUInt64(_idx_array[pos..(pos+8)].Reverse().ToArray());
                    word_data_size = BitConverter.ToUInt32(_idx_array[(pos+8)..(pos+16)].Reverse().ToArray());
                    pos += 12;
                }
                else
                {
                    word_data_offset = BitConverter.ToUInt32(_idx_array[pos..(pos+4)].Reverse().ToArray());
                    word_data_size = BitConverter.ToUInt32(_idx_array[(pos+4)..(pos+8)].Reverse().ToArray());
                    pos += 8;
                }
                Idx idx = new (word, word_data_offset, word_data_size);
                IdxData.Add(idx);
            }
        }
    }
}