namespace StarDictNet.Core;

public class Idx(string word, ulong wordDataOffset, uint wordDataSize)
{
    public string Word { get; } = word;
    public ulong WordDataOffset { get; } = wordDataOffset;
    public uint WordDataSize { get; } = wordDataSize;
}