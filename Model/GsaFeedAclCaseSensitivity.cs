// Copyright (c) 2005-2017, Coveo Solutions Inc.

using System;
using System.Xml.Serialization;

namespace GSAFeedPushConverter.Model
{
    [Serializable]
    public enum GsaFeedAclCaseSensitivity
    {
        [XmlEnum("everything-case-sensitive")]
        CaseSensitive,

        [XmlEnum("everything-case-insensitive")]
        NotCaseSensitive
    }
}
