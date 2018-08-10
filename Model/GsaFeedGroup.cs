// Copyright (c) 2005-2016, Coveo Solutions Inc.

using System;
using System.Xml.Serialization;

namespace GSAFeedPushConverter.Model
{
    [Serializable]
    [XmlRoot(ElementName = "group")]
    public class GsaFeedGroup
    {
        [XmlAttribute("action")]
        public GsaFeedRecordAction Action { get; set; }
    }
}
