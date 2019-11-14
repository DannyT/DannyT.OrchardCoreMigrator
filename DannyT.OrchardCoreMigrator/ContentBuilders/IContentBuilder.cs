using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DannyT.OrchardCoreMigrator.ContentBuilders
{
    public interface IContentBuilder
    {
        IEnumerable<JObject> GetContent();
        List<WordpressItem> WordpressItems { get; set; }
        List<WordpressCategory> WordpressCategories { get; set; }
        List<WordpressTag> WordpressTags { get; set; }
        string ParentId { get; set; }
    }
}