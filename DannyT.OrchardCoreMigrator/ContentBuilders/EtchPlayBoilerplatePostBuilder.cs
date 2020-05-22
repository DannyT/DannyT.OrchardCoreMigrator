using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DannyT.OrchardCoreMigrator.ContentBuilders {
    internal class EtchPlayBoilerplatePostBuilder : IContentBuilder {
        public string ParentId { get; set; }
        public List<WordpressItem> WordpressItems { get; set; }
        public List<WordpressCategory> WordpressCategories { get; set; }
        public List<WordpressTag> WordpressTags { get; set; }

        public IEnumerable<JObject> GetContent (IEnumerable<WordpressMedia> medias) {
            if (ParentId == string.Empty) {
                throw new Exception ("Posts for TheBlog theme require a List ID, call GetContent(string ListId) instead.");
            }
            var listMedia = medias.ToList();

            Html2EditorJSBlocks converter = new Html2EditorJSBlocks ();
            // Blog posts content items
            return from p in WordpressItems
            where p.Type == "post"
            select new JObject (
                new JProperty ("ContentItemId", $"wppost-{p.Id.ToString()}"),
                new JProperty ("ContentItemVersionId", $"wppost-{p.Id.ToString()}"),
                new JProperty ("ContentType", "NewsPost"),
                new JProperty ("DisplayText", p.Title),
                new JProperty ("Latest", true),
                new JProperty ("Published", p.Status == "publish"),
                new JProperty ("ModifiedUtc", Convert.ToDateTime (p.DatePublished)),
                new JProperty ("PublishedUtc", Convert.ToDateTime (p.DatePublished)),
                new JProperty ("CreatedUtc", Convert.ToDateTime (p.DatePublished)),
                new JProperty ("Owner", p.CreatedByUsername),
                new JProperty ("Author", p.CreatedByUsername),
                new JProperty ("TitlePart",
                    new JObject (
                        new JProperty ("Title", p.Title)
                    )
                ),
                new JProperty ("ContainedPart",
                    new JObject (
                        new JProperty ("ListContentItemId", ParentId),
                        new JProperty ("Order", 0)
                    )
                ),
                new JProperty ("AutoroutePart",
                    new JObject (
                        new JProperty ("Path", p.Link),
                        new JProperty ("SetHomepage", false)
                    )
                ),
                new JProperty ("NewsPost",
                    new JObject (
                        new JProperty ("Content",
                            new JObject (
                                new JProperty ("Data", converter.Convert (p.Content)),
                                new JProperty ("Html", null)
                            )
                        ),
                        new JProperty ("ThumbnailImage",
                            new JObject (
                                new JProperty ("Paths", new JArray(listMedia.Where(x => x.Id == p.ThumbnailId).Select(x => x.AttachmentUrl).ToArray())) // TODO: work out featured images
                            )
                        ),
                        new JProperty ("ThumbnailAlt",
                            new JObject (
                                new JProperty ("Text", $"Thumbnail Image for {p.Title}")
                            )
                        ),
                        new JProperty ("Authors",
                            new JObject(
                                new JProperty("ContentItemIds", new JArray(p.CreatedByUsername == "Will Freeman" ? "42z2wgpc7yy8aswfr9hrjqq0df" : p.CreatedByUsername == "Wesley Williams" ? "4sv2zshtvcsfzw8v7gdg84mwx7" : p.CreatedByUsername == "Anthony Haigh" ? "42yb0jxxj28gm4r5q96a7p17cb" : "4xakhby6xtp7fr2k98rssjpnne"))
                            )
                        ),
                        new JProperty ("FurtherReading",
                            new JObject (
                                new JProperty ("ContentItemIds", new JArray ()) // TODO: work out featured images
                            )
                        ),
                        new JProperty ("Category",
                            new JObject (
                                new JProperty ("ContentItemIds", new JArray (
                                    from c in p.Categories select (from cat in WordpressCategories where cat.NiceName == c select $"wpcat-{cat.Id.ToString()}").DefaultIfEmpty("").FirstOrDefault().ToString()
                                ))
                            )
                        ),
                        new JProperty ("Tags",
                            new JObject (
                                new JProperty ("ContentItemIds", new JArray (
                                    from t in p.Tags select (from tag in WordpressTags where tag.Slug == t select $"wptag-{tag.Id.ToString()}").DefaultIfEmpty("").FirstOrDefault().ToString()
                                ))
                            )
                        )
                    )
                )
            );
        }
    }
}