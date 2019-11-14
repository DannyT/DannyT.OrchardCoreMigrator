using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DannyT.OrchardCoreMigrator.ContentBuilders
{
    internal class TheBlogPageBuilder : IContentBuilder
    {
        public string ParentId { get; set; }
        public List<WordpressItem> WordpressItems { get; set; }
        public List<WordpressCategory> WordpressCategories { get; set; }
        public List<WordpressTag> WordpressTags { get; set; }

        public IEnumerable<JObject> GetContent()
        {
            return from p in WordpressItems
                   where p.Type == "page"
                   select new JObject(
                       new JProperty("ContentItemId", $"wppage-{p.Id.ToString()}"),
                       new JProperty("ContentItemVersionId", $"wppage-{p.Id.ToString()}"),
                       new JProperty("ContentType", "Page"),
                       new JProperty("DisplayText", p.Title),
                       new JProperty("Latest", true),
                       new JProperty("Published", p.Status == "publish"),
                       new JProperty("ModifiedUtc", Convert.ToDateTime(p.DatePublished)),
                       new JProperty("PublishedUtc", Convert.ToDateTime(p.DatePublished)),
                       new JProperty("CreatedUtc", Convert.ToDateTime(p.DatePublished)),
                       new JProperty("Owner", p.CreatedByUsername),
                       new JProperty("Author", p.CreatedByUsername),
                       new JProperty("Page", new JObject()),
                       new JProperty("AutoroutePart",
                            new JObject(
                               new JProperty("Path", p.Link),
                               new JProperty("SetHomepage", false)
                               )
                           ),
                       new JProperty("FlowPart",
                            new JObject(
                                new JProperty("Widgets",
                                    new JArray(
                                        new JObject(
                                            new JProperty("ContentItemId", $"wppagewidget-{p.Id.ToString()}"),
                                           new JProperty("ContentItemVersionId", $"wppagewidget-{p.Id.ToString()}"),
                                           new JProperty("ContentType", "RawHtml"),
                                           new JProperty("DisplayText", null),
                                           new JProperty("Latest", false),
                                           new JProperty("Published", false),
                                           new JProperty("ModifiedUtc", Convert.ToDateTime(p.DatePublished)),
                                           new JProperty("PublishedUtc", null),
                                           new JProperty("CreatedUtc", null),
                                           new JProperty("Owner", null),
                                           new JProperty("Author", p.CreatedByUsername),
                                           new JProperty("RawHtml",
                                                new JObject(
                                                    new JProperty("Content",
                                                        new JObject(
                                                            new JProperty("Html", p.Content)
                                                            )
                                                        )
                                                    )
                                                ),
                                           new JProperty("FlowMetadata",
                                            new JObject(
                                                new JProperty("Alignment", 3),
                                                new JProperty("Size", 100)
                                                )
                                            )
                                           )
                                        )
                                    )
                                )
                            ),
                       new JProperty("TitlePart",
                           new JObject(
                               new JProperty("Title", p.Title)
                               )
                           )
                       );
        }
    }
}