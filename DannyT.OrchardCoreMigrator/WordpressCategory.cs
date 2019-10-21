namespace DannyT.OrchardCoreMigrator
{
    public class WordpressCategory
    {
        public long Id { get; internal set; }
        public string NiceName { get; internal set; }
        public string Name { get; internal set; }

        public string ParentName { get; internal set; }
        public string Description { get; internal set; }
    }
}