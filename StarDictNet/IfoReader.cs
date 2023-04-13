using System.Text;

namespace StarDictNet
{
    public class Ifo
    {
        public string? BookName {get; set;}
        public int WordCount {get; set;}
        public int SynWordCount {get; set;} = -1;
        public int IdxFileSize {get; set;}
        public int IdxOffsetBits {get; set;} = 32;
        public string? Author {get; set;}
        public string? Email {get; set;}
        public string? Website {get; set;}
        public string? Description {get; set;}
        public string? Date {get; set;}
        public string? SameTypeSequence {get; set;}
        public string? DictType {get; set;}

        public Ifo(string path)
        {
            _populate_props(getLines(path));
        }

        public Ifo(Stream stream)
        {
            _populate_props(getLines(stream));
        }
        private IEnumerable<string> getLines(string path)
        {
            try
            {
                return File.ReadLines(path);
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Could not find/open ifo file.");
                throw;
            }
        }

        private IEnumerable<string> getLines(Stream ifo )
        {
            List<string> lines;
            try
            {
                lines = new();
                var stream = new StreamReader(ifo, Encoding.UTF8);
                string line;
                while ((line = stream.ReadLine()) != null)
                {
                    lines.Add(line);
                }
                return lines;
            }
            catch (System.Exception)
            { 
                throw;
            }
        }

        private void _populate_props(IEnumerable<string> lines)
        {
            if(lines.First() != "StarDict's dict ifo file")
            {
                Console.WriteLine("First line of ifo file does not match magic (\"StarDict's dict ifo file\").");
                throw new Exception();
            }
            foreach (var line in lines)
            {
                var _line = line.Split("=", 2);
                switch (_line[0])
                {
                    case "bookname":
                        BookName = _line[1];
                        break;
                    case "wordcount":
                        WordCount = int.Parse(_line[1]);
                        break;
                    case "synwordcount":
                        SynWordCount = int.Parse(_line[1]);
                        break;
                    case "idxfilesize":
                        IdxFileSize = int.Parse(_line[1]);
                        break;
                    case "idxoffsetbits":
                        IdxOffsetBits = int.Parse(_line[1]);
                        break;
                    case "author":
                        Author = _line[1];
                        break;
                    case "email":
                        Email = _line[1];
                        break;
                    case "website":
                        Website = _line[1];
                        break;
                    case "description":
                        Description = _line[1];
                        break;
                    case "date":
                        Date = _line[1];
                        break;
                    case "sametypesequence":
                        SameTypeSequence = _line[1];
                        break;
                    case "dicttype":
                        DictType = _line[1];
                        break;
                    default:
                        break;
                }
            }
        }
    }
}