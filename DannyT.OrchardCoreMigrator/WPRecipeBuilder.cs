using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using DannyT.OrchardCoreMigrator.ContentBuilders;
using Html2Markdown;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Slugify;

namespace DannyT.OrchardCoreMigrator
{
    public class WPRecipeBuilder : IRecipeBuilder
    {
        private readonly string creationDateTime;
        private readonly string fileToImport;
        private readonly string recipeTemplateFile;
        private readonly string workingFolder;
        private readonly string recipeFolder;
        private readonly RecipeSettings recipeSettings;
        private readonly List<WordpressItem> wordpressItems;
        private readonly List<WordpressCategory> wordpressCategories;
        private readonly List<WordpressTag> wordpressTags;
        private readonly IContentBuilder postBuilder;
        private readonly IContentBuilder pageBuilder;
        private readonly UrlCleaner urlCleaner;
        private string blogName;
        private string blogDescription;
        private IEnumerable<WordpressMedia> uploadUrls;
        private readonly List<string> failedMediaUrls;

        /// <summary>
        /// Wordpress Recipe Builder. Takes a wordpress export and creates an Orchard Core recipe.
        /// </summary>
        /// <param name="fileToImport">Path to wordpress export file</param>
        /// <param name="recipeTemplateFile">Path to template Orchard Core recipe containing required content definitions</param>
        /// <param name="workingFolder">Output folder where recipe zip will end up</param>
        public WPRecipeBuilder (string fileToImport, string recipeTemplateFile, string workingFolder, RecipeSettings recipeSettings)
        {
            creationDateTime = DateTime.UtcNow.ToString ("o");
            this.fileToImport = fileToImport;
            this.recipeTemplateFile = recipeTemplateFile;

            // Setup folder for saving files to
            this.workingFolder = workingFolder;
            recipeFolder = Path.Combine (workingFolder, "recipe");
            Directory.CreateDirectory (recipeFolder);

            this.recipeSettings = recipeSettings;

            wordpressItems = new List<WordpressItem> ();
            wordpressCategories = new List<WordpressCategory> ();
            wordpressTags = new List<WordpressTag> ();
            urlCleaner = new UrlCleaner ();
            failedMediaUrls = new List<string> ();

            // initialise content builders to reflect the desired recipe
            switch (recipeSettings.Theme)
            {
                case RecipeSettings.Themes.TheBlog:
                    postBuilder = new TheBlogPostBuilder
                    {
                        ParentId = "4m2pj0mpy25450jcz817odyhbg",
                        WordpressItems = wordpressItems,
                        WordpressCategories = wordpressCategories,
                        WordpressTags = wordpressTags
                    };
                    pageBuilder = new TheBlogPageBuilder
                    {
                        WordpressItems = wordpressItems
                    };
                    break;

                case RecipeSettings.Themes.EtchPlayBoilerplate:
                    postBuilder = new EtchPlayBoilerplatePostBuilder
                    {
                        ParentId = "49q1qde4sg4q27vb9jt1gac6w5",
                        WordpressItems = wordpressItems,
                        WordpressCategories = wordpressCategories,
                        WordpressTags = wordpressTags
                    };
                    pageBuilder = new EtchPlayBoilerplatePageBuilder
                    {
                        WordpressItems = wordpressItems
                    };
                    break;
            }
        }

        public void Build ()
        {
            Console.WriteLine ("Starting import from WordPress");

            Console.WriteLine ("Reading WP export file");

            ConsumeImport (); // reads WP file and creates list of items

            Console.WriteLine ("Tidying link formats");

            UpdatePermalinks (); // replace links with either relative versions or updated permalink structure (based on recipe settings)

            // Get all images as list of urls
            uploadUrls =
                from p in wordpressItems
            where
            p.Type == "attachment" &&
                !p.AttachmentUrl.EndsWith ("php") // TODO: make this a configurable whitelist, not blacklist
            select new WordpressMedia() { Id = p.Id, AttachmentUrl = urlCleaner.SanitiseRelativePath(p.AttachmentUrl), DownloadUrl = p.AttachmentUrl };

            Console.WriteLine ("Importing...");
            Console.WriteLine ($"{uploadUrls.Count()} images");
            Console.WriteLine ($"{wordpressCategories.Count()} categories");
            Console.WriteLine ($"{wordpressTags.Count()} tags");
            Console.WriteLine ($"{wordpressItems.Where<WordpressItem>(i => i.Type == "page").Count()} pages");
            Console.WriteLine ($"{wordpressItems.Where<WordpressItem>(i => i.Type == "post").Count()} posts");

            // Create Media Step
            JObject mediaStep = GetMedia ();

            // Create Content Step
            JObject contentStep = new JObject (
                new JProperty ("name", "content"),
                new JProperty ("data",
                    new JArray (
                        GetCategories(),
                        GetTags (),
                        pageBuilder.GetContent(uploadUrls),
                        postBuilder.GetContent(uploadUrls),
                        GetRedirects()
                    )
                )
            );

            //Console.WriteLine ("Downloading Images");
            //// Download images (credit to: https://github.com/redapollos/BulkFileDownloader)
            int retries = uploadUrls.AsParallel().WithDegreeOfParallelism(4).Sum(arg => DownloadFile(arg.DownloadUrl.ToString()));

            JArray mediaStepFiles = (JArray)mediaStep["Files"];
            JObject mediaObject = null;
            // remove failed media from mediastep
            foreach (string failedUrl in failedMediaUrls)
            {
                for (var i = mediaStepFiles.Count() - 1; i >= 0; i--)
                {
                    mediaObject = (JObject)mediaStepFiles[i];
                    if (mediaObject["SourcePath"].ToString() == urlCleaner.SanitiseRelativePath(failedUrl))
                    {
                        mediaObject.Remove();
                    }
                }
            }

            // deserialise receipe
            JObject recipeJson;
            using (StreamReader file = File.OpenText (recipeTemplateFile))
            {
                JsonSerializer serializer = new JsonSerializer ();
                recipeJson = (JObject) serializer.Deserialize (file, typeof (JObject));
            }

            // Deserialize recipe steps
            JArray steps = (JArray) recipeJson["steps"];

            // TODO: Dynamically add admin menu items, maybe?
            //JObject adminMenuBlogLink = (JObject)steps[steps.Count() - 1]["data"][0]["MenuItems"][0];
            //adminMenuBlogLink["LinkText"] = blogName;
            //adminMenuBlogLink["LinkUrl"] = $"[js: 'Admin/Contents/ContentItems/{blogId}/Display']";

            // add content
            //steps.Add(mediaStep);
            steps.Add (contentStep);

            // serialize JSON directly to a file
            Console.WriteLine ("Creating recipe json");
            string filepath = Path.Combine (recipeFolder, "recipe.json");

            using (StreamWriter file = File.CreateText (filepath))
            {
                JsonSerializer serializer = new JsonSerializer
                {
                Formatting = Newtonsoft.Json.Formatting.Indented
                };
                serializer.Serialize (file, recipeJson);
            }

            // zip json and media files
            Console.WriteLine ("Creating recipe archive");
            string zipPath = Path.Combine (workingFolder, @"recipe.zip");
            if (File.Exists (zipPath))
            {
                File.Delete (zipPath);
            }
            ZipFile.CreateFromDirectory (recipeFolder, zipPath);

            // Clean Up
            Directory.Delete (recipeFolder, true);

            Console.WriteLine ("Import complete, recipe ready.");
        }

        private IEnumerable<JObject> GetRedirects ()
        {
            return from p in wordpressItems
            where p.Type == "post" || p.Type == "page"
            select new JObject (
                new JProperty ("ContentItemId", $"redirect-{p.Id.ToString()}"),
                new JProperty ("ContentItemVersionId", $"redirect-{p.Id.ToString()}"),
                new JProperty ("ContentType", "Redirect"),
                new JProperty ("DisplayText", p.Title),
                new JProperty ("Latest", true),
                new JProperty ("Published", p.Status == "publish"),
                new JProperty ("ModifiedUtc", creationDateTime),
                new JProperty ("PublishedUtc", creationDateTime),
                new JProperty ("CreatedUtc", creationDateTime),
                new JProperty ("Owner", "WP Import"),
                new JProperty ("Author", "WP Import"),
                new JProperty ("TitlePart",
                    new JObject (
                        new JProperty ("Title", p.Title)
                    )
                ),
                new JProperty ("RedirectPart",
                    new JObject (
                        new JProperty ("FromUrl", p.OldLink),
                        new JProperty ("ToUrl", p.Link),
                        new JProperty ("IsPermanent", true)
                    )
                )
            );
        }

        private void UpdatePermalinks ()
        {
            SlugHelper.Config slugConfig = new SlugHelper.Config ();
            // remove question marks from titles (default replaces with dash)
            slugConfig.StringReplacements.Add ("?", "");
            SlugHelper helper = new SlugHelper ();

            foreach (WordpressItem item in wordpressItems.Where (p => p.Type == "post" || p.Type == "page"))
            {
                if (recipeSettings.CreateRedirects)
                {
                    item.OldLink = $"/{urlCleaner.SanitiseRelativePath(item.Link).Trim('/')}";
                    item.Link = $"{helper.GenerateSlug(item.Title)}";
                }
                else
                {
                    item.Link = urlCleaner.SanitiseRelativePath (item.Link, true);
                }
            }
        }

        private JObject GetBlog (string blogId)
        {
            return new JObject (
                new JProperty ("ContentItemId", blogId),
                new JProperty ("ContentItemVersionId", "4yvexqb7s86d9vwxnhv2udhytg"),
                new JProperty ("ContentType", "Blog"),
                new JProperty ("DisplayText", blogName),
                new JProperty ("Latest", true),
                new JProperty ("Published", true),
                new JProperty ("ModifiedUtc", creationDateTime),
                new JProperty ("PublishedUtc", creationDateTime),
                new JProperty ("CreatedUtc", creationDateTime),
                new JProperty ("Owner", "WP Import"),
                new JProperty ("Author", "WP Import"),
                new JProperty ("TitlePart",
                    new JObject (
                        new JProperty ("Title", blogName)
                    )
                ),
                new JProperty ("HtmlBodyPart",
                    new JObject (
                        new JProperty ("Html", blogDescription)
                    )
                ),
                new JProperty ("AutoroutePart",
                    new JObject (
                        new JProperty ("Path", "blog")
                    )
                ),
                new JProperty ("ListPart",
                    new JObject ()
                )
            );
        }

        private JObject GetMedia ()
        {
            return new JObject (
                new JProperty ("name", "media"),
                new JProperty ("Files",
                    new JArray (
                        from a in uploadUrls select new JObject (
                            new JProperty ("SourcePath", a.AttachmentUrl),
                            new JProperty ("TargetPath", a.AttachmentUrl)
                        )
                    )
                )
            );
        }

        private IEnumerable<JObject> GetTags ()
        {
            var tags = new List<JObject> ();
            foreach (var tag in wordpressTags)
            {
                tags.Add (CreateTag (tag));
            }

            return tags;
        }

        private JObject CreateTag (WordpressTag tag)
        {
            SlugHelper.Config slugConfig = new SlugHelper.Config ();
            // remove question marks from titles (default replaces with dash)
            slugConfig.StringReplacements.Add ("?", "");
            SlugHelper helper = new SlugHelper (slugConfig);

            return new JObject (
                new JProperty ("ContentItemId", $"wptag-{tag.Id.ToString()}"),
                new JProperty ("ContentItemVersionId", null),
                new JProperty ("ContentType", "Tag"),
                new JProperty ("DisplayText", tag.Name),
                new JProperty ("Latest", true),
                new JProperty ("Published", true),
                new JProperty ("ModifiedUtc", creationDateTime),
                new JProperty ("PublishedUtc", null),
                new JProperty ("CreatedUtc", null),
                new JProperty ("Owner", null),
                new JProperty ("Author", "WP Import"),
                new JProperty ("Tag", new JObject ()),
                new JProperty ("TitlePart", new JObject (
                    new JProperty ("Title", tag.Name)
                )),
                new JProperty ("AutoroutePart", new JObject (
                    new JProperty ("Path", $"tag/{helper.GenerateSlug(tag.Name)}"),
                    new JProperty ("SetHomepage", false)
                ))
            );
        }

        private IEnumerable<JObject> GetCategories ()
        {
            var categories = new List<JObject> ();
            foreach (var tag in wordpressCategories)
            {
                categories.Add (CreateCategory (tag));
            }

            return categories;
        }

        private JObject CreateCategory (WordpressCategory category)
        {
            SlugHelper.Config slugConfig = new SlugHelper.Config ();
            // remove question marks from titles (default replaces with dash)
            slugConfig.StringReplacements.Add ("?", "");
            SlugHelper helper = new SlugHelper (slugConfig);

            return new JObject (
                new JProperty ("ContentItemId", $"wpcat-{category.Id.ToString()}"),
                new JProperty ("ContentItemVersionId", null),
                new JProperty ("ContentType", "PostCategory"),
                new JProperty ("DisplayText", category.Name),
                new JProperty ("Latest", true),
                new JProperty ("Published", true),
                new JProperty ("ModifiedUtc", creationDateTime),
                new JProperty ("PublishedUtc", null),
                new JProperty ("CreatedUtc", null),
                new JProperty ("Owner", null),
                new JProperty ("Author", "WP Import"),
                new JProperty ("PostCategory", new JObject ()),
                new JProperty ("TitlePart", new JObject (
                    new JProperty ("Title", category.Name)
                )),
                new JProperty ("AutoroutePart", new JObject (
                    new JProperty ("Path", $"tag/{helper.GenerateSlug(category.Name)}"),
                    new JProperty ("SetHomepage", false)
                ))
            );
        }

        private void ConsumeImport ()
        {
            XmlReaderSettings settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true
            };
            using XmlReader reader = XmlReader.Create (fileToImport, settings);
            while (reader.Read ())
            {
                if (reader.IsStartElement ())
                {
                    switch (reader.Name)
                    {
                        case "title":
                            blogName = reader.ReadElementContentAsString ();
                            break;

                        case "description":
                            blogDescription = reader.ReadElementContentAsString ();
                            break;
                            // TODO: refactor item, category and tag to remove repeated code
                        case "item": // includes posts, pages, nav items and media
                            WordpressItem post = ParseItem (reader);
                            wordpressItems.Add (post);
                            break;

                        case "wp:category":
                            WordpressCategory category = ParseCategory (reader);
                            wordpressCategories.Add (category);
                            break;

                        case "wp:tag":
                            WordpressTag tag = ParseTag (reader);
                            wordpressTags.Add (tag);
                            break;

                        case "wp:author":
                        default:
                            //Console.WriteLine("Skipping node: {0}", reader.Name);
                            break;
                    }
                }
            }
        }

        private WordpressTag ParseTag (XmlReader tagReader)
        {
            WordpressTag tag = new WordpressTag ();
            while (!tagReader.EOF)
            {
                if (tagReader.IsStartElement ())
                {
                    switch (tagReader.Name)
                    {
                        case "wp:term_id":
                            tag.Id = tagReader.ReadElementContentAsLong ();
                            break;

                        case "wp:tag_slug":
                            tag.Slug = tagReader.ReadElementContentAsString ();
                            break;

                        case "wp:tag_name":
                            tag.Name = tagReader.ReadElementContentAsString ();
                            break;

                        default:
                            tagReader.Read ();
                            break;
                    }
                }
                else
                {
                    tagReader.Read ();
                }
                if (tagReader.Name == "wp:tag" && tagReader.NodeType == XmlNodeType.EndElement)
                {
                    // reached end node of tag
                    break;
                }
            }
            return tag;
        }

        private WordpressCategory ParseCategory (XmlReader categoryReader)
        {
            WordpressCategory category = new WordpressCategory ();
            while (!categoryReader.EOF)
            {
                if (categoryReader.IsStartElement ())
                {
                    switch (categoryReader.Name)
                    {
                        case "wp:term_id":
                            category.Id = categoryReader.ReadElementContentAsLong ();
                            break;

                        case "wp:category_nicename":
                            category.NiceName = categoryReader.ReadElementContentAsString ();
                            break;

                        case "wp:category_parent":
                            category.ParentName = categoryReader.ReadElementContentAsString ();
                            break;

                        case "wp:cat_name":
                            category.Name = categoryReader.ReadElementContentAsString ();
                            break;

                        case "wp:category_description":
                            category.Description = categoryReader.ReadElementContentAsString ();
                            break;

                        default:
                            categoryReader.Read ();
                            break;
                    }
                }
                else
                {
                    categoryReader.Read ();
                }
                if (categoryReader.Name == "wp:category" && categoryReader.NodeType == XmlNodeType.EndElement)
                {
                    // reached end node of category
                    break;
                }
            }

            return category;
        }

        private WordpressItem ParseItem (XmlReader itemReader)
        {
            WordpressItem post = new WordpressItem ();
            while (!itemReader.EOF)
            {
                if (itemReader.IsStartElement ())
                {
                    switch (itemReader.Name)
                    {
                        case "title":
                            post.Title = itemReader.ReadElementContentAsString ();
                            break;

                        case "link":
                            post.Link = itemReader.ReadElementContentAsString ();
                            break;

                        case "wp:post_date":
                            post.DatePublished = itemReader.ReadElementContentAsString ();
                            break;

                        case "dc:creator":
                            post.CreatedByUsername = itemReader.ReadElementContentAsString ();
                            break;

                        case "description":
                            post.Content = itemReader.ReadElementContentAsString ();
                            break;

                        case "content:encoded":
                            post.Content = itemReader.ReadElementContentAsString ();
                            break;

                        case "wp:post_id":
                            post.Id = itemReader.ReadElementContentAsLong ();
                            break;

                        case "wp:status":
                            post.Status = itemReader.ReadElementContentAsString ();
                            break;

                        case "wp:post_parent":
                            post.ParentId = itemReader.ReadElementContentAsLong ();
                            break;

                        case "wp:post_type":
                            post.Type = itemReader.ReadElementContentAsString ();
                            break;

                        case "wp:attachment_url":
                            post.AttachmentUrl = itemReader.ReadElementContentAsString ();
                            break;

                        case "category":
                            if (itemReader.GetAttribute ("domain") == "category")
                            {
                                post.AddCategory (itemReader.GetAttribute ("nicename"));
                            }
                            else
                            {
                                post.AddTag (itemReader.GetAttribute ("nicename"));
                            }
                            itemReader.Read ();
                            break;

                        case "wp:postmeta":
                            itemReader.ReadToDescendant("wp:meta_key");
                            var key = itemReader.ReadElementContentAsString();
                            var value = itemReader.ReadElementContentAsString();

                            if(key == "_thumbnail_id")
                            {
                                post.ThumbnailId = long.Parse(value);
                            }

                            break;

                        default:
                            itemReader.Read ();
                            break;
                    }
                }
                else
                {
                    itemReader.Read ();
                }
                if (itemReader.Name == "item" && itemReader.NodeType == XmlNodeType.EndElement)
                {
                    // reached end node of item
                    break;
                }
            }
            return post;
        }

        private int DownloadFile (string url)
        {
            int retries = 0;

            retry:
                try
                {
                    // setup the directory
                    char[] invalids = Path.GetInvalidFileNameChars ();

                    string tempPath = url.Substring (url.IndexOf ("//") + 2);
                    string relativeFilePath = tempPath.Substring (tempPath.IndexOf ("/") + 1);
                    string fileName = relativeFilePath.LastIndexOf ('/') > -1 ? relativeFilePath.Substring (relativeFilePath.LastIndexOf ('/') + 1) : relativeFilePath;
                    fileName = string.Join ("_", fileName.Split (invalids, StringSplitOptions.RemoveEmptyEntries)); // sanitize the filename for the OS
                    var fullDirectory = Path.Combine (this.recipeFolder, relativeFilePath.Substring (0, relativeFilePath.LastIndexOf ('/')));
                    var fullFilePath = Path.Combine (fullDirectory, fileName);

                    if (!Directory.Exists (fullDirectory))
                        Directory.CreateDirectory (fullDirectory);

                    // if the file already exists, then just skip
                    if (File.Exists (fullFilePath))
                        return retries;

                    // get the file
                    HttpWebRequest webrequest = (HttpWebRequest) WebRequest.Create (url.Replace (" ", "%20"));
                    webrequest.Timeout = 10000;
                    webrequest.ReadWriteTimeout = 10000;
                    webrequest.Proxy = null;
                    webrequest.AllowAutoRedirect = true;
                    webrequest.KeepAlive = false;

                    // get stream
                    var webresponse = (HttpWebResponse) webrequest.GetResponse ();

                    // save file to disk
                    using (Stream sr = webresponse.GetResponseStream ())
                    using (FileStream sw = File.Create (fullFilePath))
                    {
                        sr.CopyTo (sw);
                    }
                }
            catch (Exception ee)
            {
                if (ee.Message != "The remote server returned an error: (404) Not Found." &&
                    ee.Message != "The remote server returned an error: (403) Forbidden.")
                {
                    if (ee.Message.StartsWith ("The operation has timed out") ||
                        ee.Message == "Unable to connect to the remote server" ||
                        ee.Message.StartsWith ("The request was aborted: ") ||
                        ee.Message.StartsWith ("Unable to read data from the trans­port con­nec­tion: ") ||
                        ee.Message == "The remote server returned an error: (408) Request Timeout.")
                    {
                        if (retries++ < 5)
                            goto retry;
                        else
                        {
                            failedMediaUrls.Add (url);
                            Console.WriteLine ($"FAIL: {url} : {ee.Message}");
                        }
                    }
                    else
                    {
                        this.failedMediaUrls.Add (url);
                        Console.WriteLine ($"FAIL: {url} : {ee.Message}");
                    }
                }
                else
                {
                    failedMediaUrls.Add (url);
                    Console.WriteLine ($"FAIL: {url} : {ee.Message}");
                }
            }

            return retries;
        }
    }
}