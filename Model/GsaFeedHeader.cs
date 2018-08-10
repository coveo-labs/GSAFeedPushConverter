// Copyright (c) 2005-2016, Coveo Solutions Inc.

using System;
using System.Xml.Serialization;

namespace GSAFeedPushConverter.Model
{
    [Serializable]
    [XmlRoot(ElementName = "header")]
    public class GsaFeedHeader
    {
        [XmlElement("datasource")]
        public string DataSource { get; set; }

        [XmlElement("feedtype")]
        public GsaFeedType FeedType { get; set; }

        [XmlElement("organizationid")]
        public string OrganizationId { get; set; }

        [XmlElement("sourceid")]
        public string SourceId { get; set; }

        [XmlElement("apikey")]
        public string APIKey { get; set; }

    }
}
