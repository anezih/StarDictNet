// https://framagit.org/tuxor1337/dictzip.js/-/blob/main/dictzip_sync.js
using System.IO.Compression;
using System.Text;

namespace StarDictNet
{
    public class SUBFIELD
    {
        public char SI1;
        public char SI2;
        public int LEN;
        public byte[] DATA;
    }

    public class FEXTRA
    {
        public int XLEN = 0;
        public List<SUBFIELD> SUBFIELDS;
    }

    public class DZHeader
    {
        public byte ID1;
        public byte ID2;
        public byte CM;
        public byte FLG;
        public int MTIME;
        public byte XFL;
        public byte OS;
        public FEXTRA FEXTRA = new();
        public string FNAME;
        public string FCOMMENT;
        public int FHCRC;
        public int LENGTH;
    }

    public class DictZip
    {
        private Stream DictZipStream;
        public DZHeader header = new();

        private byte FTEXT = 0x01;
        private byte FHCRC = 0x02;
        private byte FEXTRA = 0x04;
        private byte FNAME = 0x08;
        private byte FCOMMENT = 0x10;

        public int VER;
        public int CHLEN;
        public int CHCOUNT;
        public List<Tuple<int, int>> CHUNKS = new();


        public DictZip(string path)
        {
            var fullPath = Path.GetFullPath(path);
            DictZipStream = File.Open(fullPath, FileMode.Open);
            readHeader();
            getChunks();
        }

        public DictZip(Stream stream)
        {
            DictZipStream = stream;
            readHeader();
            getChunks();
        }

        ~DictZip()
        {
            if (DictZipStream != null)
            {
                DictZipStream.Dispose();
            }
        }

        private byte[] byteArrayChunk(Stream stream, int offset, int chunkSize)
        {
            byte[] res = new byte[chunkSize];
            stream.Seek(offset, SeekOrigin.Begin);
            stream.Read(res, 0, chunkSize);
            return res;
        }

        private string zeroTermString(Stream stream, int offset)
        {
            var chunk = byteArrayChunk(stream, offset, 1024);
            var zero = Array.IndexOf(chunk, (byte)0);
            var res = Encoding.Latin1.GetString(chunk[0..zero]); // https://www.ietf.org/rfc/rfc1952.txt
            return res;
        }

        private void readHeader()
        {
            int pos = 0;
            var first10 = byteArrayChunk(DictZipStream, 0, 10);
            header.ID1 = first10[0];
            header.ID2 = first10[1];
            if (header.ID1 != 0x1F || header.ID2 != 0x8B)
            {
                Console.WriteLine("Not a valid gzip header.");
                throw new Exception();
            }

            header.CM     = first10[2];
            header.FLG    = first10[3];
            header.MTIME  = first10[4] << 0;
            header.MTIME |= first10[5] << 8;
            header.MTIME |= first10[6] << 16;
            header.MTIME |= first10[7] << 24;
            header.XFL    = first10[8];
            header.OS     = first10[9];
            pos += 10;

            if ((header.FLG & FEXTRA) != 0x00)
            {
                var _fextra = byteArrayChunk(DictZipStream, pos, 2);
                header.FEXTRA.XLEN = BitConverter.ToUInt16(_fextra);
                pos += 2;
            }

            var fextraSubfields = byteArrayChunk(DictZipStream, pos, header.FEXTRA.XLEN);
            List<SUBFIELD> sfds = new();
            while (true)
            {
                var len = fextraSubfields[2] + 256*fextraSubfields[3];
                SUBFIELD s = new();
                s.SI1 = Convert.ToChar(fextraSubfields[0]);
                s.SI2 = Convert.ToChar(fextraSubfields[1]);
                s.LEN = len;
                s.DATA = fextraSubfields[4..(4+len)];
                sfds.Add(s);
                fextraSubfields = fextraSubfields[(4+len)..fextraSubfields.Length];
                if(fextraSubfields.Length == 0) break;
            }
            header.FEXTRA.SUBFIELDS = sfds;
            pos += header.FEXTRA.XLEN;

            if ((header.FLG & FNAME) != 0x00)
            {
                header.FNAME = zeroTermString(DictZipStream, pos);
                pos += header.FNAME.Length + 1;
            }

            if ((header.FLG & FCOMMENT) != 0x00)
            {
                header.FCOMMENT = zeroTermString(DictZipStream, pos);
                pos += header.FCOMMENT.Length;
            }

            if ((header.FLG & FHCRC) != 0x00)
            {
                var fextra = byteArrayChunk(DictZipStream, pos, 2);
                header.FHCRC = BitConverter.ToUInt16(fextra);
                pos += 2;
            }

            header.LENGTH = pos;
        }
        private void getChunks()
        {
            var subfields = header.FEXTRA.SUBFIELDS;
            bool found = false;
            SUBFIELD sf = new();
            foreach (var item in subfields)
            {
                if (item.SI1 == 'R' || item.SI2 == 'A')
                {
                    found = true;
                    sf = item;
                    break;
                }
            }

            if (!found)
            {
                Console.WriteLine("Unsupported gzip header.");
                throw new Exception();
            }
            else
            {
                var data = sf.DATA;
                VER     = data[0] + 256 * data[1];
                CHLEN   = data[2] + 256 * data[3];
                CHCOUNT = data[4] + 256 * data[5];

                for (int i = 0, chpos = 0; i < CHCOUNT && 2*i + 6 < data.Length; i++)
                {
                    var tmp_chlen = data[2*i + 6] + 256*data[2*i+7];
                    Tuple<int, int> pair = new(chpos, tmp_chlen);
                    CHUNKS.Add(pair);
                    chpos += tmp_chlen;
                }
            }
        }

        public string readAt(int pos, int len)
        {
            var firstChunk = Math.Min((pos/CHLEN), CHUNKS.Count-1);
            var lastChunk = Math.Min(((pos+len)/CHLEN), CHUNKS.Count-1);
            var offset = pos - (firstChunk * CHLEN);
            var finish = offset + len;

            List<byte[]> outBuf = new();
            byte[] inBuf = byteArrayChunk(
                DictZipStream,
                header.LENGTH + CHUNKS[firstChunk].Item1,
                header.LENGTH + CHUNKS[lastChunk].Item1 + CHUNKS[lastChunk].Item2
            );
            for (
                int i = firstChunk, j = 0;
                i <= lastChunk && j < inBuf.Length;
                j += CHUNKS[i].Item2, i++
            )
            {
                var chunk = inBuf[j..(j+CHUNKS[i].Item2)];
                MemoryStream msIn = new(chunk);
                MemoryStream msOut = new();
                var decomp = new DeflateStream(msIn, CompressionMode.Decompress);
                decomp.CopyTo(msOut);
                var inflated = msOut.ToArray();
                outBuf.Add(inflated);
            }
            var final = outBuf.SelectMany(b => b).ToArray()[offset..finish];
            return Encoding.UTF8.GetString(final);
        }
    }
}