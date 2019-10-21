using System;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;
using Html2Markdown;
using System.IO.Compression;
using System.Net;

namespace DannyT.OrchardCoreMigrator
{
    public class WPRecipeBuilder : IRecipeBuilder
    {
        private readonly string creationDateTime;
        private readonly string fileToImport;
        private readonly string recipeTemplateFile;
        private readonly string workingFolder;
        private readonly string recipeFolder;
        private readonly List<WordpressItem> wordpressItems;
        private readonly List<WordpressCategory> wordpressCategories;
        private readonly List<WordpressTag> wordpressTags;
        private string blogName;
        private string blogDescription;
        private IEnumerable<string> uploadUrls;

        /// <summary>
        /// Wordpress Recipe Builder. Takes a wordpress export and creates an Orchard Core recipe.
        /// </summary>
        /// <param name="fileToImport">Path to wordpress export file</param>
        /// <param name="recipeTemplateFile">Path to template Orchard Core recipe containing required content definitions</param>
        /// <param name="workingFolder">Output folder where recipe zip will end up</param>
        public WPRecipeBuilder(string fileToImport, string recipeTemplateFile, string workingFolder)
        {
            creationDateTime = DateTime.UtcNow.ToString("o");
            this.fileToImport = fileToImport;
            this.recipeTemplateFile = recipeTemplateFile;

            // Setup folder for saving files to
            this.workingFolder = workingFolder;
            recipeFolder = Path.Combine(workingFolder, "recipe");
            Directory.CreateDirectory(recipeFolder);

            wordpressItems = new List<WordpressItem>();
            wordpressCategories = new List<WordpressCategory>();
            wordpressTags = new List<WordpressTag>();
        }


        public void Build()
        {
            Console.WriteLine("Starting import from WordPress");

            Console.WriteLine("Reading WP export file");
            
            ConsumeImport(); // reads WP file and creates list of items

            // Get all images as list of urls
            uploadUrls =
                from p in wordpressItems
                where
                    p.Type == "attachment" &&
                    !p.AttachmentUrl.EndsWith("php") // TODO: make this a configurable whitelist, not blacklist
                select new string(p.AttachmentUrl);

            Console.WriteLine("Importing...");
            Console.WriteLine($"{uploadUrls.Count()} images");
            Console.WriteLine($"{wordpressCategories.Count()} categories");
            Console.WriteLine($"{wordpressTags.Count()} tags");
            Console.WriteLine($"{wordpressItems.Where<WordpressItem>(i => i.Type == "page").Count()} pages");
            Console.WriteLine($"{wordpressItems.Where<WordpressItem>(i => i.Type == "post").Count()} posts");

            // Create Media Step
            JObject mediaStep = GetMedia();

            // Create Content Step
            string blogId = "4m2pj0mpy25450jcz817odyhbg";
            JObject contentStep = new JObject(
                new JProperty("name", "content"),
                new JProperty("data",
                    new JArray(
                        GetCategories(),
                        GetTags(),
                        GetPages(),
                        GetBlog(blogId),
                        GetPosts(blogId)
                    )
                )
            );

            Console.WriteLine("Downloading Images");
            // Download images (credit to: https://github.com/redapollos/BulkFileDownloader)
            int retries = uploadUrls.AsParallel().WithDegreeOfParallelism(4).Sum(arg => DownloadFile(arg));

            // deserialise receipe
            JObject recipeJson;
            using (StreamReader file = File.OpenText(recipeTemplateFile))
            {
                JsonSerializer serializer = new JsonSerializer();
                recipeJson = (JObject)serializer.Deserialize(file, typeof(JObject));
            }

            // Deserialize recipe steps
            JArray steps = (JArray)recipeJson["steps"];

            // update admin menu blog link
            JObject adminMenuBlogLink = (JObject)steps[steps.Count() - 1]["data"][0]["MenuItems"][0];
            adminMenuBlogLink["LinkText"] = blogName;
            adminMenuBlogLink["LinkUrl"] = $"[js: 'Admin/Contents/ContentItems/{blogId}/Display']";

            // add content
            steps.Add(mediaStep);
            steps.Add(contentStep);

            // serialize JSON directly to a file
            Console.WriteLine("Creating recipe json");
            string filepath = Path.Combine(recipeFolder, "recipe.json");

            using (StreamWriter file = File.CreateText(filepath))
            {
                JsonSerializer serializer = new JsonSerializer
                {
                    Formatting = Newtonsoft.Json.Formatting.Indented
                };
                serializer.Serialize(file, recipeJson);
            }

            // zip json and media files
            Console.WriteLine("Creating recipe archive");
            string zipPath = Path.Combine(workingFolder, @"recipe.zip");
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            ZipFile.CreateFromDirectory(recipeFolder, zipPath);

            // Clean Up
            Directory.Delete(recipeFolder, true);

            Console.WriteLine("Import complete, recipe ready.");
        }

        private IEnumerable<JObject> GetPages()
        {
            return from p in wordpressItems
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
                               new JProperty("Path", SanitiseRelativePath(p.Link)), // TODO: option to set new autoroute and create 403 from old path
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
        

        private IEnumerable<JObject> GetPosts(string blogId)
        {
            Converter markdownConverter = new Converter();
            // Blog posts content items
            return from p in wordpressItems
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
                               new JProperty("ListContentItemId", blogId),
                               new JProperty("Order", 0)
                               )
                           ),
                       new JProperty("MarkdownBodyPart",
                           new JObject(
                               new JProperty("Markdown", p.Content)//markdownConverter.Convert(p.Content)) // TODO: support html parts as well
                               )
                           ),
                       new JProperty("AutoroutePart",
                           new JObject(
                               new JProperty("Path", SanitiseRelativePath(p.Link)), // TODO: option to set new autoroute and create 403 from old path
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
                                        select (from cat in wordpressCategories
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
                                        select (from tag in wordpressTags
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

        private JObject GetBlog(string blogId)
        {
            return new JObject(
                new JProperty("ContentItemId", blogId),
                new JProperty("ContentItemVersionId", "4yvexqb7s86d9vwxnhv2udhytg"),
                new JProperty("ContentType", "Blog"),
                new JProperty("DisplayText", blogName),
                new JProperty("Latest", true),
                new JProperty("Published", true),
                new JProperty("ModifiedUtc", creationDateTime),
                new JProperty("PublishedUtc", creationDateTime),
                new JProperty("CreatedUtc", creationDateTime),
                new JProperty("Owner", "WP Import"),
                new JProperty("Author", "WP Import"),
                new JProperty("TitlePart",
                    new JObject(
                        new JProperty("Title", blogName)
                        )
                ),
                new JProperty("HtmlBodyPart",
                    new JObject(
                        new JProperty("Html", blogDescription)
                        )
                ),
                new JProperty("AutoroutePart",
                    new JObject(
                        new JProperty("Path", "blog")
                        )
                ),
                new JProperty("ListPart",
                    new JObject()
                )
            );
        }

        private JObject GetMedia()
        {
            return new JObject(
                new JProperty("name", "media"),
                new JProperty("Files",
                    new JArray(
                        from a in uploadUrls
                        select new JObject(
                            new JProperty("SourcePath", SanitiseRelativePath(a)),
                            new JProperty("TargetPath", SanitiseRelativePath(a))
                        )
                    )
                )
            );
        }

        private string SanitiseRelativePath(string url)
        {
            // remove domain part of URL
            string tempPath = url.Substring(url.IndexOf("//") + 2);
            string relativeFilePath = tempPath.Substring(tempPath.IndexOf("/") + 1);

            return relativeFilePath;
        }

        private JObject GetTags()
        {
            return new JObject(
                new JProperty("ContentItemId", "49ymvebjd46550a9z95j4udiej"),
                new JProperty("ContentItemVersionId", "4s62f1v0kq6hc36t412d87yy56"),
                new JProperty("ContentType", "Taxonomy"),
                new JProperty("DisplayText", "Tags"),
                new JProperty("Latest", true),
                new JProperty("Published", true),
                new JProperty("ModifiedUtc", creationDateTime),
                new JProperty("PublishedUtc", creationDateTime),
                new JProperty("CreatedUtc", creationDateTime),
                new JProperty("Owner", "WP Import"),
                new JProperty("Author", "WP Import"),
                new JProperty("TitlePart",
                    new JObject(
                        new JProperty("Title", "Tags")
                        )
                ),
                new JProperty("AliasPart",
                    new JObject(
                        new JProperty("Alias", "tags")
                        )
                ),
                new JProperty("TaxonomyPart",
                    new JObject(
                        new JProperty("Terms",
                            new JArray(
                                from t in wordpressTags
                                select CreateTag(t)
                            )
                        ),
                        new JProperty("TermContentType", "Tag")
                    )
                )
            );
        }

        private object CreateTag(WordpressTag tag)
        {
            return new JObject(
                            new JProperty("ContentItemId", $"wptag-{tag.Id.ToString()}"),
                            new JProperty("ContentItemVersionId", null),
                            new JProperty("ContentType", "Tag"),
                            new JProperty("DisplayText", tag.Name),
                            new JProperty("Latest", false),
                            new JProperty("Published", false),
                            new JProperty("ModifiedUtc", creationDateTime),
                            new JProperty("PublishedUtc", null),
                            new JProperty("CreatedUtc", null),
                            new JProperty("Owner", null),
                            new JProperty("Author", "WP Import"),
                            new JProperty("Tag", new JObject()),
                            new JProperty("TitlePart", new JObject(
                                new JProperty("Title", tag.Name)
                            ))
                        );
        }

        private JObject GetCategories()
        {
            return new JObject(
                new JProperty("ContentItemId", "4zwnd978ed66tvxj1cb69mbc5z"),
                new JProperty("ContentItemVersionId", "491favk9tc5mdv9btqw5wn1gcb"),
                new JProperty("ContentType", "Taxonomy"),
                new JProperty("DisplayText", "Categories"),
                new JProperty("Latest", true),
                new JProperty("Published", true),
                new JProperty("ModifiedUtc", creationDateTime),
                new JProperty("PublishedUtc", creationDateTime),
                new JProperty("CreatedUtc", creationDateTime),
                new JProperty("Owner", "WP Import"),
                new JProperty("Author", "WP Import"),
                new JProperty("TitlePart",
                    new JObject(
                        new JProperty("Title", "Categories")
                        )
                ),
                new JProperty("AliasPart",
                    new JObject(
                        new JProperty("Alias", "categories")
                        )
                ),
                new JProperty("TaxonomyPart",
                    new JObject(
                        new JProperty("Terms",
                            new JArray(
                                from c in wordpressCategories
                                where c.ParentName == string.Empty
                                select CreateCategory(c)
                            )
                        ),
                        new JProperty("TermContentType", "Category")
                    )
                )
            );
        }

        private JObject CreateCategory(WordpressCategory category)
        {
            return new JObject(
                            new JProperty("ContentItemId", $"wpcat-{category.Id.ToString()}"),
                            new JProperty("ContentItemVersionId", null),
                            new JProperty("ContentType", "Category"),
                            new JProperty("DisplayText", category.Name),
                            new JProperty("Latest", false),
                            new JProperty("Published", false),
                            new JProperty("ModifiedUtc", creationDateTime),
                            new JProperty("PublishedUtc", null),
                            new JProperty("CreatedUtc", null),
                            new JProperty("Owner", null),
                            new JProperty("Author", "WP Import"),
                            new JProperty("Category", new JObject()),
                            new JProperty("TitlePart", new JObject(
                                new JProperty("Title", category.Name)
                            )),
                            new JProperty("Terms", new JArray(
                                from child in wordpressCategories
                                where child.ParentName == category.NiceName
                                select CreateCategory(child)
                                )
                            )
                        );
        }

        private void ConsumeImport()
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            using XmlReader reader = XmlReader.Create(fileToImport, settings);
            while (reader.Read())
            {
                if (reader.IsStartElement())
                {
                    switch (reader.Name)
                    {
                        case "title":
                            blogName = reader.ReadElementContentAsString();
                            break;
                        case "description":
                            blogDescription = reader.ReadElementContentAsString();
                            break;
                        // TODO: refactor item, category and tag to remove repeated code
                        case "item": // includes posts, pages, nav items and media
                            WordpressItem post = ParseItem(reader);
                            wordpressItems.Add(post);
                            break;
                        case "wp:category":
                            WordpressCategory category = ParseCategory(reader);
                            wordpressCategories.Add(category);
                            break;
                        case "wp:tag":
                            WordpressTag tag = ParseTag(reader);
                            wordpressTags.Add(tag);
                            break;
                        case "wp:author":
                        default:
                            //Console.WriteLine("Skipping node: {0}", reader.Name);
                            break;
                    }
                }
            }
        }

        private WordpressTag ParseTag(XmlReader tagReader)
        {
            WordpressTag tag = new WordpressTag();
            while (!tagReader.EOF)
            {
                if (tagReader.IsStartElement())
                {
                    switch (tagReader.Name)
                    {
                        case "wp:term_id":
                            tag.Id = tagReader.ReadElementContentAsLong();
                            break;
                        case "wp:tag_slug":
                            tag.Slug = tagReader.ReadElementContentAsString();
                            break;
                        case "wp:tag_name":
                            tag.Name = tagReader.ReadElementContentAsString();
                            break;
                        default:
                            tagReader.Read();
                            break;
                    }
                }
                else
                {
                    tagReader.Read();
                }
                if (tagReader.Name == "wp:tag" && tagReader.NodeType == XmlNodeType.EndElement)
                {
                    // reached end node of tag
                    break;
                }
            }
            return tag;
        }

        private WordpressCategory ParseCategory(XmlReader categoryReader)
        {
            WordpressCategory category = new WordpressCategory();
            while (!categoryReader.EOF)
            {
                if (categoryReader.IsStartElement())
                {
                    switch (categoryReader.Name)
                    {
                        case "wp:term_id":
                            category.Id = categoryReader.ReadElementContentAsLong();
                            break;
                        case "wp:category_nicename":
                            category.NiceName = categoryReader.ReadElementContentAsString();
                            break;
                        case "wp:category_parent":
                            category.ParentName = categoryReader.ReadElementContentAsString();
                            break;
                        case "wp:cat_name":
                            category.Name = categoryReader.ReadElementContentAsString();
                            break;
                        case "wp:category_description":
                            category.Description = categoryReader.ReadElementContentAsString();
                            break;
                        default:
                            categoryReader.Read();
                            break;
                    }
                }
                else
                {
                    categoryReader.Read();
                }
                if (categoryReader.Name == "wp:category" && categoryReader.NodeType == XmlNodeType.EndElement)
                {
                    // reached end node of category
                    break;
                }
            }

            return category;
        }

        private WordpressItem ParseItem(XmlReader itemReader)
        {
            WordpressItem post = new WordpressItem();
            while (!itemReader.EOF)
            {
                if (itemReader.IsStartElement())
                {
                    switch (itemReader.Name)
                    {
                        case "title":
                            post.Title = itemReader.ReadElementContentAsString();
                            break;
                       
                        case "link":
                            post.Link = itemReader.ReadElementContentAsString();
                            break;
                        
                        case "wp:post_date":
                            post.DatePublished = itemReader.ReadElementContentAsString();
                            break;
                        
                        case "dc:creator":
                            post.CreatedByUsername = itemReader.ReadElementContentAsString();
                            break;

                        case "description":
                            post.Content = itemReader.ReadElementContentAsString();
                            break;

                        case "content:encoded":
                            post.Content = itemReader.ReadElementContentAsString();
                            break;

                        case "wp:post_id":
                            post.Id = itemReader.ReadElementContentAsLong();
                            break;

                        case "wp:status":
                            post.Status = itemReader.ReadElementContentAsString();
                            break;

                        case "wp:post_parent":
                            post.ParentId = itemReader.ReadElementContentAsLong();
                            break;

                        case "wp:post_type":
                            post.Type = itemReader.ReadElementContentAsString();
                            break;

                        case "wp:attachment_url":
                            post.AttachmentUrl = itemReader.ReadElementContentAsString();
                            break;
                        case "category":
                            if(itemReader.GetAttribute("domain") == "category")
                            {
                                post.AddCategory(itemReader.GetAttribute("nicename"));
                            }
                            else
                            {
                                post.AddTag(itemReader.GetAttribute("nicename"));
                            }
                            itemReader.Read();
                            break;
                        default:
                            itemReader.Read();
                            break;
                    }
                }
                else
                {
                    itemReader.Read();
                }
                if (itemReader.Name == "item" && itemReader.NodeType == XmlNodeType.EndElement)
                {
                    // reached end node of item
                    break;
                }
            }
            return post;
        }

        private int DownloadFile(string url)
        {
            int retries = 0;

            retry:
            try
            {
                // setup the directory
                char[] invalids = Path.GetInvalidFileNameChars();

                string tempPath = url.Substring(url.IndexOf("//") + 2);
                string relativeFilePath = tempPath.Substring(tempPath.IndexOf("/") + 1);
                string fileName = relativeFilePath.LastIndexOf('/') > -1 ? relativeFilePath.Substring(relativeFilePath.LastIndexOf('/') + 1) : relativeFilePath;
                fileName = string.Join("_", fileName.Split(invalids, StringSplitOptions.RemoveEmptyEntries)); // sanitize the filename for the OS
                var fullDirectory = Path.Combine(this.recipeFolder, relativeFilePath.Substring(0, relativeFilePath.LastIndexOf('/')));
                var fullFilePath = Path.Combine(fullDirectory, fileName);

                if (!Directory.Exists(fullDirectory))
                    Directory.CreateDirectory(fullDirectory);

                // if the file already exists, then just skip
                if (File.Exists(fullFilePath))
                    return retries;

                // get the file
                HttpWebRequest webrequest = (HttpWebRequest)WebRequest.Create(url.Replace(" ", "%20"));
                webrequest.Timeout = 10000;
                webrequest.ReadWriteTimeout = 10000;
                webrequest.Proxy = null;
                webrequest.AllowAutoRedirect = true;
                webrequest.KeepAlive = false;

                // get stream
                var webresponse = (HttpWebResponse)webrequest.GetResponse();

                // save file to disk
                using (Stream sr = webresponse.GetResponseStream())
                using (FileStream sw = File.Create(fullFilePath))
                {
                    sr.CopyTo(sw);
                }
            }

            catch (Exception ee)
            {
                if (ee.Message != "The remote server returned an error: (404) Not Found." &&
                    ee.Message != "The remote server returned an error: (403) Forbidden.")
                {
                    if (ee.Message.StartsWith("The operation has timed out") ||
                        ee.Message == "Unable to connect to the remote server" ||
                        ee.Message.StartsWith("The request was aborted: ") ||
                        ee.Message.StartsWith("Unable to read data from the trans­port con­nec­tion: ") ||
                        ee.Message == "The remote server returned an error: (408) Request Timeout.")
                    {
                        if (retries++ < 5)
                            goto retry;
                        else
                            Console.WriteLine($"FAIL: {url} : {ee.Message}");
                    }
                    else
                    {
                        Console.WriteLine($"FAIL: {url} : {ee.Message}");
                    }
                }
            }

            return retries;
        }
    }
}
