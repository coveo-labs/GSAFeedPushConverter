// Copyright (c) 2005-2017, Coveo Solutions Inc.

using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace GSAFeedPushConverter.Model
{
    [Serializable]
    [XmlRoot(ElementName = "principal", IsNullable = false)]
    public class GsaFeedPrincipal
    {
        [XmlAttribute("scope")]
        public GsaFeedAclScope AclScope { get; set; }

        [XmlAttribute("access")]
        public GsaFeedAclAccess Access { get; set; }

        [XmlAttribute("namespace"), DefaultValue("Default")]
        public string Namespace { get; set; }

        [XmlAttribute("case-sensitivity-type"), DefaultValue(GsaFeedAclCaseSensitivity.CaseSensitive)]
        public GsaFeedAclCaseSensitivity CaseSensitivityType { get; set; }

        [XmlAttribute("principal-type")]
        public string PrincipalType { get; set; }

        [XmlText()]
        public string Value { get; set; }
    }
}
