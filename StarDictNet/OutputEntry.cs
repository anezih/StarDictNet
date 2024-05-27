using System.Text;

namespace StarDictNet;

public class OutputEntry
{
    readonly Encoding utf8NoBom = new UTF8Encoding(false);
    internal int idx = 0;
    internal int defOffset = 0;
    public string Headword { get; set; }
    public HashSet<string>? Alternatives { get; set; }
    public string Definition { get; set; }

    public OutputEntry(string headWord, string definition)
    {
        this.Headword = headWord.Trim();
        this.Definition = definition.Trim();
    }

    public OutputEntry(string headWord, string definition, HashSet<string> alternatives)
    {
        this.Headword = headWord.Trim();
        this.Definition = definition.Trim();
        this.Alternatives = alternatives;

        this.Alternatives.Remove(this.Headword);
    }

    public byte[] HeadwordUTF8() => utf8NoBom.GetBytes(Headword);

    public byte[] DefinitionUTF8() => utf8NoBom.GetBytes(Definition);

    public int DefinitionSize() => DefinitionUTF8().Length;
}