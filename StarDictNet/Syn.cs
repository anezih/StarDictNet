namespace StarDictNet
{  
    public class Syn
    {
        public string SynWord { get; }
        public uint OriginalWordIndex { get; }

        public Syn(string syn_word, uint owi)
        {
            SynWord = syn_word;
            OriginalWordIndex = owi;
        }
    }
}