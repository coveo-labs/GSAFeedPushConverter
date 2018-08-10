// Copyright (c) 2005-2017, Coveo Solutions Inc.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace GSAFeedPushConverter.Model
{
    public class GsaFeedParser
    {
        private const string HEADER_ELEMENT = "header";
        private const string GROUP_ELEMENT = "group";
        private const string RECORD_ELEMENT = "record";
        private const string ACTION_ATTRIBUTE = "action";
        private const string ACL_ELEMENT = "acl";
        private const string XML_GROUP_ELEMENT = "xmlgroups";
        private const string MEMBERSHIP_ELEMENT = "membership";

        private readonly string m_FeedFilePath;

        /// <summary>
        /// The url of the acl with it is ACL. It can be use to get the parent acl or the item acl set before.
        /// </summary>
        private static Dictionary<string, GsaFeedAcl> aclInheritanceDictionary = new Dictionary<string, GsaFeedAcl>();

        public GsaFeedParser(string p_FeedFilePath)
        {
            m_FeedFilePath = p_FeedFilePath;
        }

        public IEnumerable<GsaFeedRecord> ParseFeedRecords()
        {
            CheckFileExists(m_FeedFilePath);
            bool isReadToGroup = false;

            using (XmlReader reader = CreateXmlReader(m_FeedFilePath)) {
                reader.MoveToContent();
                while (reader.Read()) {
                    try
                    {
                        isReadToGroup = reader.ReadToFollowing(GROUP_ELEMENT);
                    } catch (Exception e)
                    {
                        isReadToGroup = false;
                    }

                    if (isReadToGroup)
                    {
                        if (reader.LocalName == GROUP_ELEMENT)
                        {
                            string xmlAction = reader.GetAttribute(ACTION_ATTRIBUTE);
                            GsaFeedRecordAction groupAction = GsaFeedRecordAction.Unspecified;

                            if (!String.IsNullOrWhiteSpace(xmlAction))
                            {
                                groupAction = (GsaFeedRecordAction)Enum.Parse(typeof(GsaFeedRecordAction),
                                    xmlAction);
                            }

                            //TODO ajouter aussi les xmlgroups dans le parsing
                            XElement xmlRecord = null;
                            if (reader.ReadToFollowing(RECORD_ELEMENT))
                            {
                                do
                                {
                                    try
                                    {
                                        xmlRecord = XNode.ReadFrom(reader) as XElement;
                                        //var xmlRecord = reader.ReadInnerXml();
                                    }
                                    catch (Exception e)
                                    {
                                        Program.m_Logger.Error("Failed to read record: " + e.Message);
                                        Program.s_Response.statusCode = System.Net.HttpStatusCode.NotAcceptable;
                                        Program.s_Response.reason = "Failed to read record: " + e.Message;
                                        continue;
                                    }

                                    var record = Deserialize<GsaFeedRecord>(xmlRecord);

                                    //TODO look at the doc to make sure that the record cannot override the group action
                                    if (groupAction != GsaFeedRecordAction.Unspecified)
                                    {
                                        record.Action = groupAction;
                                    }

                                    //We add the record to the dictionary of parents
                                    if (record.Acl != null)
                                    {
                                        //we will need the document url in the Acl to construct the permissions.
                                        record.Acl.DocumentUrl = record.Url;
                                        //We set the acl in the dictionnary and construct the inheritance
                                        record.Acl = ConstructAclInheritance(record.Acl);
                                    }
                                    else if (aclInheritanceDictionary.ContainsKey(record.Url))
                                    {
                                        record.Acl = aclInheritanceDictionary[record.Url];
                                    }

                                    yield return record;
                                } while (reader.ReadToNextSibling(RECORD_ELEMENT));
                            }
                            else if (reader.ReadToDescendant(ACL_ELEMENT))
                            {
                            }
                        }
                        else if (reader.LocalName == RECORD_ELEMENT)
                        {
                        }
                    }
                }
            }
        }

        public IEnumerable<GsaFeedAcl> ParseFeedAcl()
        {
            CheckFileExists(m_FeedFilePath);

            using (XmlReader reader = CreateXmlReader(m_FeedFilePath)) {
                while (reader.Read()) {
                    if (reader.NodeType == XmlNodeType.Element) {
                        if (reader.LocalName == GROUP_ELEMENT) {
                            //TODO ajouter aussi les xmlgroups dans le parsing
                            bool hasAclElement = false;
                            try
                            {
                                hasAclElement = reader.ReadToFollowing(ACL_ELEMENT);
                            } catch (Exception e)
                            {
                                Program.m_Logger.Fatal("Failed to forward to ACL element: "+e.Message);
                                //yield break;
                            }
                            if (hasAclElement) {
                                do {
                                    var xmlAcl = XNode.ReadFrom(reader) as XElement;
                                    var acl = Deserialize<GsaFeedAcl>(xmlAcl);
                                    acl = ConstructAclInheritance(acl);
                                    yield return acl;
                                } while (reader.ReadToNextSibling(ACL_ELEMENT));
                            }
                        }
                    }
                }
            }
        }

        public GsaFeedHeader ParseFeedHeader()
        {
            CheckFileExists(m_FeedFilePath);

            GsaFeedHeader header = null;

            try
            {
                using (XmlReader reader = CreateXmlReader(m_FeedFilePath))
                {
                    XElement xmlHeader = null;
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.LocalName == HEADER_ELEMENT)
                        {
                            xmlHeader = XNode.ReadFrom(reader) as XElement;
                            header = Deserialize<GsaFeedHeader>(xmlHeader);
                            break;
                        }
                    }
                }
            } catch (Exception e)
            {
                Program.m_Logger.Fatal("Malformed XML header detected.");
                Program.s_Response.statusCode = System.Net.HttpStatusCode.NotAcceptable;
                Program.s_Response.reason = "Failed to read header: " + e.Message;
            }

            return header;
        }

        public IEnumerable<GsaFeedMembership> ParseFeedGroups()
        {
            CheckFileExists(m_FeedFilePath);

            using (XmlReader reader = CreateXmlReader(m_FeedFilePath)) {
                while (reader.Read()) {
                    if (reader.NodeType == XmlNodeType.Element) {
                        if (reader.LocalName == XML_GROUP_ELEMENT) {
                            if (reader.ReadToDescendant(MEMBERSHIP_ELEMENT)) {
                                do {
                                    //TODO si on veut supporter les groupes voir le case-sensitive parce que dans les groupes toutes semble ne CAPS
                                    var xmlgroups = XNode.ReadFrom(reader) as XElement;
                                    GsaFeedMembership membership = Deserialize<GsaFeedMembership>(xmlgroups);
                                    yield return membership;
                                } while (reader.ReadToNextSibling(MEMBERSHIP_ELEMENT));
                            }
                        }
                    }
                }
            }
        }

        private GsaFeedAcl ConstructAclInheritance(GsaFeedAcl acl)
        {
            /*
             * Note: If a per-URL ACL inherits from a non-existent URL, or inherits from a URL that does not have a per-URL ACL,
             * the authorization decision is always INDETERMINATE because of the broken inheritance chain.
             * https://www.google.com/support/enterprise/static/gsa/docs/admin/72/gsa_doc_set/feedsguide/feedsguide.html#1084377
             */
            //we try to get the parent acl to construct the inheritance later on.
            acl.ParentAcl = GetAclInDictionary(acl.InheritFrom);

            aclInheritanceDictionary[acl.DocumentUrl] = acl;
            return acl;
        }

        /// <summary>
        /// Try to get the Acl in the dictionary and return null if the Acl is not found.
        /// </summary>
        /// <param name="p_AclUrl">The url of the Acl to get</param>
        /// <returns>The asked Acl</returns>
        private GsaFeedAcl GetAclInDictionary(string p_AclUrl)
        {
            GsaFeedAcl acl = null;
            if (p_AclUrl != null) {
                aclInheritanceDictionary.TryGetValue(p_AclUrl, out acl);
            }
            return acl;
        }

        private static void CheckFileExists(string p_FeedFilePath)
        {
            if (String.IsNullOrWhiteSpace(p_FeedFilePath) || !File.Exists(p_FeedFilePath)) {
                throw new ArgumentException(String.Format("The file '{0}' does not exists.", p_FeedFilePath));
            }
        }

        private static XmlReader CreateXmlReader(string p_FeedFilePath)
        {
            XmlReader reader = XmlReader.Create(new StreamReader(p_FeedFilePath, Encoding.GetEncoding("UTF-8")), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });

            //XmlReader reader = XmlReader.Create(p_FeedFilePath,
            //    new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
            return reader;
        }

        private static T Deserialize<T>(XElement p_Element) where T : class
        {
            var serializer = new XmlSerializer(typeof(T));
            return (T) serializer.Deserialize(p_Element.CreateReader());
        }
    }
}
