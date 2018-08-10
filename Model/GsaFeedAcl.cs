// Copyright (c) 2005-2017, Coveo Solutions Inc.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace GSAFeedPushConverter.Model
{
    [Serializable]
    [XmlRoot(ElementName = "acl")]
    public class GsaFeedAcl
    {
        [XmlAttribute("url")]
        public string DocumentUrl { get; set; }

        [XmlAttribute("inheritance-type"), DefaultValue(GsaFeedAclInheritance.LeafNode)]
        public GsaFeedAclInheritance InheritanceType { get; set; }
        
        [XmlAttribute("inherit-from")]
        public string InheritFrom { get; set; }

        [XmlElement("principal")]
        public List<GsaFeedPrincipal> Principals { get; set; }

        public GsaFeedAcl ParentAcl { get; set; }
    }
}
