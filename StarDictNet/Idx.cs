namespace StarDictNet
{
    public class Idx
    {
        public string Word { get; }
        public ulong WordDataOffset { get; }
        public uint WordDataSize { get; }

        public Idx(string word, ulong wdo, uint wds)
        {
            Word = word;
            WordDataOffset = wdo;
            WordDataSize = wds;
        }
    }
}