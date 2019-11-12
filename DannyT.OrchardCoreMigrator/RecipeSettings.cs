namespace DannyT.OrchardCoreMigrator
{
    public class RecipeSettings
    {
        public Themes Theme { get; set; }

        public enum Themes 
        { 
            TheBlog,
            EtchPlayBoilerplate
        }
    }
}