using System.Text;

namespace StarDictNet;

public class OutputEntry
{
    internal int idx = 0;
    internal int defOffset = 0;
    public string Headword { get; set; }
    public HashSet<string>? Alternatives { get; set; }
    public string Definition { get; set; }

    public OutputEntry(string headWord, string definition)
    {
        this.Headword = headWord;
        this.Definition = definition;
    }

    public OutputEntry(string headWord, string definition, HashSet<string> alternatives)
    {
        this.Headword = headWord;
        this.Definition = definition;
        this.Alternatives = alternatives;
    }

    public byte[] HeadwordUTF8() => Encoding.UTF8.GetBytes(Headword);

    public byte[] DefinitionUTF8() => Encoding.UTF8.GetBytes(Definition);

    public int DefinitionSize() => DefinitionUTF8().Length;
}