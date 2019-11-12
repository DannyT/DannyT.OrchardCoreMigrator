using System;
using System.Collections.Generic;
using System.Linq;
using Html2Markdown;
using Newtonsoft.Json.Linq;

namespace DannyT.OrchardCoreMigrator.ContentBuilders
{
    internal class TheBlogPostBuilder : IContentBuilder
    {
        public string ParentId { get; set; }
        public List<WordpressItem> WordpressItems { get; set; }
        public List<WordpressCategory> WordpressCategories { get; set; }
        public List<WordpressTag> WordpressTags { get; set; }
        public UrlCleaner UrlCleaner { get; set; }

        public IEnumerable<JObject> GetContent()
        {
            if(ParentId == string.Empty) { 
                throw new Exception("Posts for TheBlog theme require a List ID, call GetContent(string ListId) instead.");
            }
            Converter markdownConverter = new Converter();
            // Blog posts content items
            return from p in WordpressItems
                   where p.Type == "post"
                   select new JObject(
                       new JProperty("ContentItemId", $"wppost-{p.Id.ToString()}"),
                       new JProperty("ContentItemVersionId", $"wppost-{p.Id.ToString()}"),
                       new JProperty("ContentType", "BlogPost"),
                       new JProperty("DisplayText", p.Title),
                       new JProperty("Latest", true),
                       new JProperty("Published", p.Status == "publish"),
                       new JProperty("ModifiedUtc", Convert.ToDateTime(p.DatePublished)),
                       new JProperty("PublishedUtc", Convert.ToDateTime(p.DatePublished)),
                       new JProperty("CreatedUtc", Convert.ToDateTime(p.DatePublished)),
                       new JProperty("Owner", p.CreatedByUsername),
                       new JProperty("Author", p.CreatedByUsername),
                       new JProperty("TitlePart",
                           new JObject(
                               new JProperty("Title", p.Title)
                               )
                           ),
                       new JProperty("ContainedPart",
                           new JObject(
                               new JProperty("ListContentItemId", ParentId),
                               new JProperty("Order", 0)
                               )
                           ),
                       new JProperty("MarkdownBodyPart",
                           new JObject(
                               new JProperty("Markdown", markdownConverter.Convert(p.Content))
                               )
                           ),
                       new JProperty("AutoroutePart",
                           new JObject(
                               new JProperty("Path", UrlCleaner.SanitiseRelativePath(p.Link)), // TODO: option to set new autoroute and create 403 from old path
                               new JProperty("SetHomepage", false)
                               )
                           ),
                       new JProperty("BlogPost",
                           new JObject(
                               new JProperty("Subtitle",
                                   new JObject(
                                       new JProperty("Text", null)
                                   )
                               ), new JProperty("Categories",
                            new JObject(
                                new JProperty("TermContentItemIds",
                                    new JArray(
                                        from c in p.Categories
                                        select (from cat in WordpressCategories
                                                where cat.NiceName == c
                                                select $"wpcat-{cat.Id.ToString()}").FirstOrDefault().ToString()
                                            )
                                    ),
                                new JProperty("TaxonomyContentItemId", "4zwnd978ed66tvxj1cb69mbc5z") // TODO: make variable
                            )
                        ),
                       new JProperty("Tags",
                            new JObject(
                                new JProperty("TermContentItemIds",
                                    new JArray(
                                        from t in p.Tags
                                        select (from tag in WordpressTags
                                                where tag.Slug == t
                                                select $"wptag-{tag.Id.ToString()}").FirstOrDefault().ToString()
                                            )
                                    ),
                                new JProperty("TaxonomyContentItemId", "49ymvebjd46550a9z95j4udiej") // TODO: make variable
                            )
                        )
                    )
                )
            );
        }
    }
}