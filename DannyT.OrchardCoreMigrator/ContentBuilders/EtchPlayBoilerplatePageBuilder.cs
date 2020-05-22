using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace DannyT.OrchardCoreMigrator.ContentBuilders
{
    internal class EtchPlayBoilerplatePageBuilder : IContentBuilder
    {
        public string ParentId { get; set; }
        public List<WordpressItem> WordpressItems { get; set; }
        public List<WordpressCategory> WordpressCategories { get; set; }
        public List<WordpressTag> WordpressTags { get; set; }

        public IEnumerable<JObject> GetContent(IEnumerable<WordpressMedia> medias)
        {
            var regex = new Regex(@"\[[^]]*\]");
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
                       new JProperty("Content",
                            new JObject(
                                new JProperty("ContentItems",
                                    new JArray(
                                        new JObject(
                                            new JProperty("ContentItemId", $"wppagewidget-{p.Id.ToString()}"),
                                           new JProperty("ContentItemVersionId", $"wppagewidget-{p.Id.ToString()}"),
                                           new JProperty("ContentType", "Section"),
                                           new JProperty("DisplayText", "Imported Content"),
                                           new JProperty("Latest", false),
                                           new JProperty("Published", false),
                                           new JProperty("ModifiedUtc", Convert.ToDateTime(p.DatePublished)),
                                           new JProperty("PublishedUtc", null),
                                           new JProperty("CreatedUtc", null),
                                           new JProperty("Owner", null),
                                           new JProperty("Author", p.CreatedByUsername),
                                           new JProperty("Section",
                                           new JArray(
                                                new JObject(
                                                    new JProperty("BackgroundColour",
                                                        new JObject(
                                                            new JProperty("Text", "default")
                                                            )
                                                        )
                                                    ),
                                                new JObject(
                                                    new JProperty("Alignment",
                                                        new JObject(
                                                            new JProperty("Text", "default")
                                                            )
                                                        )
                                                    )
                                                )
                                           ),
                                            new JProperty("TitlePart",
                                                new JObject(
                                                    new JProperty("Title", "Imported Content")
                                                )
                                           ),
                                            new JProperty("Children",
                                            new JObject(
                                                new JProperty("ContentItems",
                                                    new JArray(
                                                        new JObject(
                                                            new JProperty("ContentItemId", $"wppagewidget-{p.Id.ToString()}"),
                                                           new JProperty("ContentItemVersionId", $"wppagewidget-{p.Id.ToString()}"),
                                                           new JProperty("ContentType", "Html"),
                                                           new JProperty("DisplayText", "Imported Content"),
                                                           new JProperty("Latest", false),
                                                           new JProperty("Published", false),
                                                           new JProperty("ModifiedUtc", Convert.ToDateTime(p.DatePublished)),
                                                           new JProperty("PublishedUtc", null),
                                                           new JProperty("CreatedUtc", null),
                                                           new JProperty("Owner", null),
                                                           new JProperty("Author", p.CreatedByUsername),
                                                           new JProperty("Html",
                                                                new JObject(
                                                                    new JProperty("Body",
                                                                        new JObject(
                                                                            new JProperty("Html", regex.Replace(p.Content, string.Empty))
                                                                            )
                                                                        )
                                                                    )
                                                                ),
                                                            new JProperty("TitlePart",
                                                                new JObject(
                                                                    new JProperty("Title", "Imported Content")
                                                                )
                                                           )
                                                            )
                                                        )
                                                    )
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