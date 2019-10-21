using System;
using System.IO;
using DannyT.OrchardCoreMigrator;

namespace wordpress_to_orchardcore
{
    class Program
    {
        static int Main(string[] args)
        {
            string wordpressExport;
            if (args.Length == 0)
            {
                Console.WriteLine("No wordpress export provided, using example file. To specify an export use:");
                Console.WriteLine(@"    wordpress-to-orchardcore.exe ""c:\path\to\wordpress-export.xml""");
                wordpressExport = @"Assets\example-wordpress-export.xml";
            }
            else
            {
                wordpressExport = args[0].ToString();
                Console.WriteLine($"Using wordpress export file: {wordpressExport}");
            }
            
            string templateRecipe = @"Assets\TemplateRecipe.json";
            string workingFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDoc‌​uments), "WpToOc");

            IRecipeBuilder recipeBuilder = new WPRecipeBuilder(wordpressExport, templateRecipe, workingFolder);
            recipeBuilder.Build();
            System.Diagnostics.Process.Start("explorer.exe", workingFolder);
            return 0;
        }
    }
}
