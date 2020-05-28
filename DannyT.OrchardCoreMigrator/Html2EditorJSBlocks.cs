using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace DannyT.OrchardCoreMigrator
{
    public class Html2EditorJSBlocks
    {
        public Html2EditorJSBlocks()
        {
        }

        internal string Convert(string content)
        {
            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            string cleansedHtml = StripWidthHeightFromImages(content);
            cleansedHtml = cleansedHtml.Replace("<p></p>", string.Empty);

            string[] lines = cleansedHtml.Split(
                new[] { "\r\n\r\n", "\r\r", "\n\n", "\n" },
                StringSplitOptions.None
            );

            List<object> blocks = new List<object>();
            for (var i = 0; i < lines.Count(); i++)
            {
                var line = lines[i];
                if (line.StartsWith("<h")) // heading
                {
                    blocks.Add(new
                    {
                        type = "header",
                        data = new
                        {
                            text = StripHtml(line),
                            level = line.Substring(2, 1)
                        }
                    });
                }
                else if (line.StartsWith("<"))
                {
                    if (line.StartsWith("<!--"))
                    {
                        continue;
                    }

                    if(line.StartsWith("<p>"))
                    {
                        blocks.Add(new
                        {
                            type = "paragraph",
                            data = new
                            {
                                text = line
                            }
                        });

                        continue;
                    }



                    blocks.Add(new
                    {
                        type = "raw",
                        data = new
                        {
                            html = line
                        }
                    });
                }
                else if (line.StartsWith("https://www.youtu")
                    || line.StartsWith("http://www.youtu")
                    || line.StartsWith("https://youtu")
                    || line.StartsWith("http://youtu"))
                {
                    blocks.Add(new
                    {
                        type = "embed",
                        data = new
                        {
                            service = "youtube",
                            source = line,
                            embed = $"//www.youtube.com/embed/{GetYoutubeId(new Uri(line))}",
                            width = 580,
                            height = 320,
                            caption = ""

                        }
                    });
                }
                else
                {
                    if(string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    blocks.Add(new
                    {
                        type = "paragraph",
                        data = new
                        {
                            text = line
                        }
                    });
                }
            }

            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var time = System.Convert.ToInt64((DateTime.Now - epoch).TotalSeconds);

            return JObject.FromObject(new
            {
                time,
                blocks,
                version = "2.15.0"
            }).ToString(Newtonsoft.Json.Formatting.None);
        }

        private string StripHtml(string value)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(value);
            return WebUtility.HtmlDecode(doc.DocumentNode.InnerText);
        }

        private string StripWidthHeightFromImages(string value)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(value);
            var images = doc.DocumentNode.SelectNodes("//img");
            if(images != null)
            {
                foreach (HtmlNode image in images)
                {
                    if (image.Attributes["width"] != null)
                    {
                        image.Attributes["width"].Remove();
                    }
                    if (image.Attributes["height"] != null)
                    {
                        image.Attributes["height"].Remove();
                    }
                }
            }

            return WebUtility.HtmlDecode(doc.DocumentNode.InnerHtml);
        }

        private string GetYoutubeId(Uri uri)
        {

            string YoutubeLinkRegex = "(?:.+?)?(?:\\/v\\/|watch\\/|\\?v=|\\&v=|youtu\\.be\\/|\\/v=|^youtu\\.be\\/)([a-zA-Z0-9_-]{11})+";
            Regex regexExtractId = new Regex(YoutubeLinkRegex, RegexOptions.Compiled);
            string[] validAuthorities = { "youtube.com", "www.youtube.com", "youtu.be", "www.youtu.be" };
        
            try
            {
                string authority = new UriBuilder(uri).Uri.Authority.ToLower();

                //check if the url is a youtube url
                if (validAuthorities.Contains(authority))
                {
                    //and extract the id
                    var regRes = regexExtractId.Match(uri.ToString());
                    if (regRes.Success)
                    {
                        return regRes.Groups[1].Value;
                    }
                }
            }
            catch { }


            return null;
        
        }
    }
}