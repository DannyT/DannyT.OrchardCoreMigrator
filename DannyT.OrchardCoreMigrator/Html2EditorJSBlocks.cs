using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

            string[] lines = content.Split(
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

                    blocks.Add(new
                    {
                        type = "raw",
                        data = new
                        {
                            html = line
                        }
                    });
                }
                else
                {
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
    }
}