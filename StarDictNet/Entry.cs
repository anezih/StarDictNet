namespace StarDictNet
{
    public class Entry
    {
        public string Word { get; set; }
        public HashSet<string> Alternatives { get; set; }
        public string Definition { get; set; }
    }
}