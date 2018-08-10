// Copyright (c) 2005-2016, Coveo Solutions Inc.

using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Newtonsoft.Json.Linq;

namespace GSAFeedPushConverter.Model
{
    [Serializable]
    [XmlRoot(ElementName = "record")]
    public class GsaFeedRecord
    {
        [XmlAttribute("last-modified")]
        public string LastModifiedString { get; set; }

        [XmlIgnore]
        public DateTime? LastModified
        {
            get
            {
                if (!String.IsNullOrWhiteSpace(LastModifiedString)) {
                    return DateTime.Parse(LastModifiedString);
                }

                return null;
            }
        }

        [XmlAttribute("mimetype")]
        public string MimeType { get; set; }

        [XmlAttribute("url")]
        public string Url { get; set; }

        [XmlAttribute("displayurl")]
        public string DisplayUrl { get; set; }

        [XmlAttribute("action")]
        public GsaFeedRecordAction Action { get; set; }

        [XmlAttribute("authmethod")]
        public GsaFeedRecordAuthMethod AuthMethod { get; set; }

        [XmlAttribute("lock")]
        public bool Lock { get; set; }

        [XmlAttribute("pagerank")]
        public string PageRank { get; set; }

        [XmlAttribute("crawl-immediately")]
        public bool CrawlImmediately { get; set; }

        [XmlAttribute("crawl-once")]
        public bool CrawlOnce { get; set; }

        [XmlAttribute("scoring")]
        public string Scoring { get; set; }

        [XmlElement("content")]
        public GsaFeedContent Content { get; set; }

        [XmlElement("metadata")]
        public GsaFeedMetadata Metadata { get; set; }

        [XmlElement("attachments")]
        public GsaFeedAttachments Attachments { get; set; }

        [XmlElement("acl")]
        public GsaFeedAcl Acl { get; set; }

        public IDictionary<string, JToken> ConvertMetadata()
        {
            IDictionary<string, JToken> metadata = new Dictionary<string, JToken>();

            if (Metadata != null) {
                foreach (GsaFeedMeta meta in Metadata.Values) {
                    if (metadata.ContainsKey(meta.Name)) {
                        metadata[meta.GetDecodedName()] = String.Join(";", metadata[meta.GetDecodedName()], meta.GetDecodedContent());
                    } else {
                        metadata.Add(meta.GetDecodedName(), meta.GetDecodedContent());
                    }
                }
            }

            return metadata;
        }
    }
}
