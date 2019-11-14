namespace DannyT.OrchardCoreMigrator
{
    public class RecipeSettings
    {
        public RecipeSettings()
        {
            CreateRedirects = false;
            PermalinkStructure = "yyyy/MM/dd";
        }

        public Themes Theme { get; set; }

        public enum Themes 
        { 
            TheBlog,
            EtchPlayBoilerplate
        }

        public string PermalinkStructure { get; set; }
        public bool CreateRedirects { get; set; }
    }
}