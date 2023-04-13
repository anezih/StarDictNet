namespace StarDictNet
{
    public class CompressionOptions
    {
        public bool IDX = false;
        public bool SYN = false;
        public bool DICT = true;

        public CompressionOptions(bool idx, bool syn, bool dict)
        {
            IDX = idx;
            SYN = syn;
            DICT = dict;
        }
        public CompressionOptions(bool idx, bool dict)
        {
            IDX = idx;
            DICT = dict;
        }

        public CompressionOptions(){}
    }
}