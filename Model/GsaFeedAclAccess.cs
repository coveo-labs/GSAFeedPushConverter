﻿// Copyright (c) 2005-2017, Coveo Solutions Inc.

using System;
using System.Xml.Serialization;

namespace GSAFeedPushConverter.Model
{
    [Serializable]
    public enum GsaFeedAclAccess
    {
        [XmlEnum("permit")]
        Permit,

        [XmlEnum("deny")]
        Deny
    }
}
