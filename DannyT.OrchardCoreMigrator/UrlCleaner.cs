namespace DannyT.OrchardCoreMigrator
{
    public class UrlCleaner
    {
        public string SanitiseRelativePath(string url, bool stripTrailingSlashes = false)
        {
            // remove domain part of URL
            string tempPath = url.Substring(url.IndexOf("//") + 2);
            string relativeFilePath = tempPath.Substring(tempPath.IndexOf("/") + 1);
            if(stripTrailingSlashes)
            {
                relativeFilePath = relativeFilePath.TrimEnd('/');
            }

            return relativeFilePath;
        }
    }
}