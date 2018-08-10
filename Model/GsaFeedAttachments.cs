// Copyright (c) 2005-2016, Coveo Solutions Inc.

using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace GSAFeedPushConverter.Model
{
    [Serializable]
    [XmlRoot(ElementName = "attachments")]
    public class GsaFeedAttachments
    {
        [XmlElement("path")]
        public string Value { get; set; }
    }
}
