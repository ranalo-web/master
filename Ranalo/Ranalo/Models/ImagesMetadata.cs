namespace Ranalo.Models
{
    public class ImagesMetadata
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string FileName { get; set; }
        public string Url { get; set; }
        public string File { get; set; }
        public string Type { get; set; }
        public int Size { get; set; }
    }

    public class MetaDataEntry
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
    }
}
