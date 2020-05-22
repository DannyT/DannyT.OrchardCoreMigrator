using System;
using System.Collections.Generic;

namespace DannyT.OrchardCoreMigrator
{
    public class WordpressItem
    {
        public WordpressItem()
        {
            Categories = new List<string>();
            Tags = new List<string>();
        }

        public string Title { get; internal set; }
        public string Link { get; internal set; }
        public string OldLink { get; internal set; } // used to create redirects
        public string DatePublished { get; internal set; }
        public string CreatedByUsername { get; internal set; }
        public string Content { get; internal set; }
        public string Exceprt { get; internal set; }
        public long Id { get; internal set; }
        public string AttachmentUrl { get; internal set; }
        public long ParentId { get; internal set; }
        public string Type { get; internal set; }
        public string Status { get; internal set; }
        public List<string> Categories { get; private set; }
        public List<string> Tags { get; private set; }

        public long ThumbnailId { get; set; }

        internal void AddCategory(string category)
        {
            Categories.Add(category);
        }

        internal void AddTag(string tag)
        {
            Tags.Add(tag);
        }
    }
}