// Copyright (c) 2005-2017, Coveo Solutions Inc.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Coveo.Connectors.Utilities;
using Coveo.Connectors.Utilities.PushApiSdk;
using Coveo.Connectors.Utilities.PushApiSdk.Config;
using Coveo.Connectors.Utilities.PushApiSdk.Helpers;
using Coveo.Connectors.Utilities.PushApiSdk.Manager;
using Coveo.Connectors.Utilities.PushApiSdk.Model;
using Coveo.Connectors.Utilities.PushApiSdk.Model.Document;
using Coveo.Connectors.Utilities.PushApiSdk.Model.Permission;
using GSAFeedPushConverter.Model;
using GSAFeedPushConverter.Utilities;
using Newtonsoft.Json.Linq;
using Ionic.Zip;
using System.IO.Compression;
using Ionic.Zlib;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace GSAFeedPushConverter
{
    internal class Program
    {
        private const string FEEDTYPE = "feedtype";
        private const string DATASOURCE = "datasource";
        public static readonly string ALLOW_GROUP = "-allowed";
        public static readonly string DISALLOW_GROUP = "-disallowed";

        private const string m_ConfigFilePath = "ConnectorConfig.json";

        private static HttpDownloader s_HttpDownloader;
        public static Configuration s_Configuration;
        public static G2CoveoResponse s_Response;
        public static SimpleLogger m_Logger;

        private static void Main(string[] p_Args)
        {
            Startup.Start();

            s_HttpDownloader = new HttpDownloader();
            s_Configuration = new Configuration();
            s_Response = new G2CoveoResponse();

            //First of all, we load the connector configuration file.
            if (!File.Exists(m_ConfigFilePath))
            {
                //We cannot work without a configuration file.
                m_Logger.Fatal("ConnectorConfig.json file not found, closing now.");
                return;
            } else if (!UpdateAndValidateConfig()) {
                //Something is not right with the configuration file.
                m_Logger.Fatal("ConnectorConfig.json file has definition error(s), closing now.");
                return;
            }

            // Only create a new log file if one hasn't been created today
            m_Logger = new SimpleLogger(s_Configuration, true);

            string ListeningRecordsUrl = "http://" + s_Configuration.ListeningHost + ":" + s_Configuration.ListeningPort + "/xmlfeed/";
            string ListeningGroupsUrl = "http://" + s_Configuration.ListeningHost + ":" + s_Configuration.ListeningPort + "/xmlgroups/";

            //Make sure the TEMP_FOLDER has been created before we start
            try
            {
                Directory.CreateDirectory(s_Configuration.TempFolder);
            } catch
            {
                m_Logger.Fatal("Not able to create TEMP_FOLDER.");
                return;
            }

            WebServer webServer = new WebServer(ProcessRequestForFeed, ProcessFeed, ListeningRecordsUrl);
            webServer.Run();
            WebServer webServerGroups = new WebServer(ProcessRequestForGroups, ProcessGroups, ListeningGroupsUrl);
            webServerGroups.Run();

            Console.ReadKey();

            s_HttpDownloader.Dispose();
            webServer.Stop();
            webServerGroups.Stop();
        }

        private static FeedRequestResponse ProcessRequestForGroups(HttpListenerRequest p_HttpRequest)
        {
            return ProcessRequest(p_HttpRequest, true);
        }

        private static FeedRequestResponse ProcessRequestForFeed(HttpListenerRequest p_HttpRequest)
        {
            return ProcessRequest(p_HttpRequest, false);
        }


        private static FeedRequestResponse ProcessRequest(HttpListenerRequest p_HttpRequest,
            bool isGroups)
        {
            FeedRequestResponse response = new FeedRequestResponse();

            if (s_Response.statusCode != HttpStatusCode.OK)
            {
                response.HttpStatusCode = s_Response.statusCode;
                response.Message = s_Response.reason;
            } else
            {
                response.HttpStatusCode = HttpStatusCode.OK;
                if (isGroups)
                {
                    response.Message = String.Format("Processed GSA XML Groups");
                }
                else
                {
                    response.Message = String.Format("Processed GSA Feed");
                }
            }

            return response;
        }

        private static void ProcessGroups(HttpListenerRequest p_HttpRequest)
        {
            if (p_HttpRequest != null && p_HttpRequest.HttpMethod == HttpMethod.Post.Method.ToUpperInvariant()) {
                ConsoleUtilities.WriteLine("Processing the GSA XML Groups.", ConsoleColor.Green);
                string datasource = p_HttpRequest.QueryString.Get(DATASOURCE);
                string filename = String.Format("{0}_{1}_Groups.xml", DateTime.Now.Ticks, Guid.NewGuid());
                string feedFilePath = Path.Combine(@s_Configuration.TempFolder, filename);

                using (FileStream output = File.OpenWrite(feedFilePath)) {
                    p_HttpRequest.InputStream.CopyTo(output);
                }

                GsaFeedParser parser = new GsaFeedParser(feedFilePath);
                ICoveoPushApiConfig clientConfig = new CoveoPushApiConfig(s_Configuration.PushApiEndpointUrl, s_Configuration.PlatformApiEndpointUrl, s_Configuration.ApiKey, s_Configuration.OrganizationId);
                ICoveoPushApiClient client = new CoveoPushApiClient(clientConfig);

                Console.WriteLine();
                Console.WriteLine("--------------------------------------------------------");
                Console.WriteLine("Organization: {0}", s_Configuration.OrganizationId);
                Console.WriteLine("Security provider: {0}", s_Configuration.ProviderId);
                Console.WriteLine("Push source: {0}", datasource);
                Console.WriteLine("Feed File: '{0}'", feedFilePath);
                Console.WriteLine("Records:");

                int nbOfGroups = 0;
                foreach (GsaFeedMembership gsaFeedMembership in parser.ParseFeedGroups()) {
                    nbOfGroups++;
                    PushMemberOfGroup(client.PermissionManager, s_Configuration.ProviderId, gsaFeedMembership);
                }

                Console.WriteLine();
                ConsoleUtilities.WriteLine("The XML Groups was processed.", ConsoleColor.Green);
                Console.WriteLine();
                Console.WriteLine("Statistics:");
                ConsoleUtilities.WriteLine("> Added/Updated groups: {0}", ConsoleColor.Cyan, nbOfGroups);
                //File.Delete(feedFilePath);
            } else {
                if (p_HttpRequest == null) {
                    ConsoleUtilities.WriteError("No HTTP request to process.");
                    s_Response.statusCode = HttpStatusCode.NoContent;
                    s_Response.reason = "No HTTP request to process.";
                } else {
                    ConsoleUtilities.WriteError("Invalid received request: {1} - {0}.", p_HttpRequest.HttpMethod, p_HttpRequest.Url);
                    s_Response.statusCode = HttpStatusCode.BadRequest;
                    s_Response.reason = "Invalid received request: "+ p_HttpRequest.HttpMethod +" - "+ p_HttpRequest.Url;
                }
            }
        }

        public static string LoadFeed(HttpListenerRequest p_HttpRequest)
        {
            bool isFeedLoaded = false;

            string filename = String.Format("{0}.xml", Guid.NewGuid());
            string feedFilePath = Path.Combine(@s_Configuration.TempFolder, filename);
            Console.WriteLine("feedFilePath: " + feedFilePath);

            try
            {
                using (Stream output = File.OpenWrite(feedFilePath))
                {
                    using (Stream input = p_HttpRequest.InputStream)
                    {
                        byte[] buffer = new byte[8192];
                        byte[] xmlStartBytes = { 0x3C, 0x3F, 0x78, 0x6D, 0x6C, 0x20 }; // <?xml 
                        byte[] xmlEndBytes = { 0x3C, 0x2F, 0x67, 0x73, 0x61, 0x66, 0x65, 0x65, 0x64, 0x3E }; // </gsafeed> 
                        int bytesRead;
                        int offset = 0;
                        int xmlOffset = 0;
                        bool xmlStartFound = false;

                        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            offset = 0;
                            // Before writing the buffer, move the position to the start of the XML
                            // if we haven't already found the start
                            if (!xmlStartFound)
                            {
                                xmlOffset = search(buffer, xmlStartBytes);

                                if (xmlOffset >= 0)
                                {
                                    xmlStartFound = true;
                                    offset = xmlOffset;
                                    bytesRead -= offset;
                                }
                            }
                            xmlOffset = search(buffer, xmlEndBytes);
                            if (xmlOffset >= 0)
                            {
                                // This means the end of the feed was found in the buffer. Set the 
                                // length to the position of the end of the feed
                                bytesRead = (xmlOffset - offset) + 10;
                            }

                            // Clean the buffer to ensure we have only valid XML characters
                            byte[] cleanBuffer = cleanXmlBytes(buffer, offset, bytesRead);

                            output.Write(cleanBuffer, offset, bytesRead);
                        }
                    }
                }
                isFeedLoaded = true;
            }
            catch (Exception e)
            {
                m_Logger.Fatal("Failed to load XML to "+ feedFilePath);
                feedFilePath = null;
            }

            return feedFilePath;
        }

        private static void ProcessFeed(HttpListenerRequest p_HttpRequest)
        {
            s_Response.statusCode = HttpStatusCode.OK;
            s_Response.reason = "";

            if (p_HttpRequest != null && p_HttpRequest.HttpMethod == HttpMethod.Post.Method.ToUpperInvariant()) {

                ConsoleUtilities.WriteLine("Processing the GSA Feed.", ConsoleColor.Green);

                string sourceId = s_Configuration.SourceId;
                string orgId = s_Configuration.OrganizationId;
                string apiKey = s_Configuration.ApiKey;

                // Read in the query string parameters to optionally allow the post to direct the push.
                // The SourceId is not optional.
                foreach ( string q_key in p_HttpRequest.QueryString.AllKeys)
                {
                    if (string.Equals(q_key, "sourceid", StringComparison.OrdinalIgnoreCase)) {
                        sourceId = p_HttpRequest.QueryString.Get(q_key);
                    } else if (string.Equals(q_key, "orgid", StringComparison.OrdinalIgnoreCase)) {
                        orgId = p_HttpRequest.QueryString.Get(q_key);
                    } else if (string.Equals(q_key, "apikey", StringComparison.OrdinalIgnoreCase)) {
                        apiKey = p_HttpRequest.QueryString.Get(q_key);
                    }
                    else if (string.Equals(q_key, "providerid", StringComparison.OrdinalIgnoreCase)) {
                        s_Configuration.ProviderId = p_HttpRequest.QueryString.Get(q_key);
                    }
                }

                if (sourceId != null)
                {
                    string feedFilePath = p_HttpRequest.Headers.Get("FeedFilePath");
                    GsaFeedParser parser = new GsaFeedParser(feedFilePath);
                    GsaFeedHeader header = parser.ParseFeedHeader();

                    if (header == null)
                    {
                        m_Logger.Fatal("Malformed XML exception, aborting.");
                        return;
                    }

                    string datasource = header.DataSource;
                    string feedtype = header.FeedType.ToString();

                    ICoveoPushApiConfig clientConfig = new CoveoPushApiConfig(s_Configuration.PushApiEndpointUrl, s_Configuration.PlatformApiEndpointUrl, apiKey, orgId);
                    ICoveoPushApiClient client = new CoveoPushApiClient(clientConfig);

                    m_Logger.Info("--------------------------------------------------------");
                    m_Logger.Info("Organization: "+ orgId);
                    m_Logger.Info("Security provider: "+ s_Configuration.ProviderId);
                    m_Logger.Info("Push source: "+ sourceId);
                    m_Logger.Info("Datasource: "+ datasource + ", Type: "+ feedtype);
                    m_Logger.Info("Feed File: "+ feedFilePath);
                    m_Logger.Info("Records:");

                    int addedDocs = 0;
                    int deletedDocs = 0;
                    int ignoredDocs = 0;

                    ulong orderingIdRef = 0;
                    ulong firstOrderingIdRef = 0;

                    try
                    {
                        client.ActivityManager.UpdateSourceStatus(sourceId, header.FeedType == GsaFeedType.Full ? SourceStatusType.Refresh : SourceStatusType.Incremental);
                    } catch(Exception e) {
                        m_Logger.Error("Failed to update source status: " + e.Message);
                    }

                    //foreach (GsaFeedAcl acl in parser.ParseFeedAcl()) {
                    //    Console.WriteLine(acl.ToString());
                    //}

                    foreach (GsaFeedRecord record in parser.ParseFeedRecords())
                    {
                        ConsoleUtilities.WriteLine("{4}>>> {0}|{1}|{2}|{3}",
                            record.Action == GsaFeedRecordAction.Delete ? ConsoleColor.Yellow : ConsoleColor.Cyan,
                            record.Action.ToString().ToUpperInvariant(),
                            record.Url, record.MimeType ?? "None",
                            record.LastModified?.ToUniversalTime().ToString(CultureInfo.InvariantCulture) ?? "Unspecified",
                            DateTime.Now);
                        m_Logger.Info("Processing: " + record.Action.ToString().ToUpperInvariant() + " " + record.Url);

                        if ((record.Action == GsaFeedRecordAction.Add) || (record.Action == GsaFeedRecordAction.Unspecified)) {
                            try
                            {
                                //We need to push the acl virtual groups
                                PushGroupFromAcl(client.PermissionManager, s_Configuration.ProviderId, record.Acl);
                                orderingIdRef = client.DocumentManager.AddOrUpdateDocument(sourceId,
                                    CreateDocumentFromRecord(record, header.FeedType == GsaFeedType.MetadataAndUrl),
                                    null);
                                addedDocs++;
                                m_Logger.Info("Success: " + record.Action.ToString().ToUpperInvariant() + " " + record.Url);
                            }
                            catch (Exception e)
                            {
                                m_Logger.Error("Failed to add item: " + record.Url);
                                m_Logger.Error("Reason: " + e.Message);
                            }
                        } else if (record.Action == GsaFeedRecordAction.Delete) {
                            record.Url = record.Url.Replace("&", "|");
                            orderingIdRef = client.DocumentManager.DeleteDocument(sourceId, record.Url, null);
                            deletedDocs++;
                        } else {
                            m_Logger.Error("No action was specified for the record " + record.Url);
                            ignoredDocs++;
                        }

                        if (firstOrderingIdRef == 0)
                            firstOrderingIdRef = orderingIdRef;

                        // Note that each record may contain attachments which also need to be pushed with the metadata of their parent
                        // record. This version supports an ATTACHMENTS tag to specify the path where the attachment files can be found.
                        if (record.Attachments != null)
                        {
                            // Create a duplicate of the parent record and update the body based on the file input
                            GsaFeedRecord attachRecord = record;
                            string recordBaseUrl = record.Url;
                            
                            // Get a list of files that should be pushed as part of this record.
                            string dirPath = record.Attachments.Value;
                            m_Logger.Info("Processing attachment folder: " + dirPath);

                            //if (Directory.Exists(dirPath))
                            //{
                                try
                                {
                                    string[] fileList = Directory.GetFiles(dirPath);
                                    foreach (string filePath in fileList)
                                    {
                                        m_Logger.Info("Processing: " + attachRecord.Action.ToString().ToUpperInvariant() + " " + filePath);
                                        string sourceFileName = Path.GetFileName(@filePath);
                                        string sourceFileExt = Path.GetExtension(@filePath);
                                        string sourceFile = @filePath;
                                        string targetFile = Path.Combine(@s_Configuration.TempFolder, String.Format("{0}_{1}.{2}", sourceFileName, Guid.NewGuid(), sourceFileExt));
                                        int buffLength = 24 * 1024; //24KB
                                        byte[] buff = new byte[buffLength];
                                        if ((attachRecord.Action == GsaFeedRecordAction.Delete))
                                        {
                                            attachRecord.Url = recordBaseUrl + "|" + sourceFileName;
                                            attachRecord.Url = attachRecord.Url.Replace("&", "|");
                                            orderingIdRef = client.DocumentManager.DeleteDocument(sourceId, attachRecord.Url, null);
                                            deletedDocs++;
                                        }
                                        else
                                        {
                                            try
                                            {
                                                using (FileStream sourceStream = File.OpenRead(sourceFile))
                                                {
                                                    using (FileStream targetStream = File.OpenWrite(targetFile))
                                                    {
                                                        using (ZlibStream zipStream = new ZlibStream(targetStream, Ionic.Zlib.CompressionMode.Compress))
                                                        {
                                                            int offset = 0;
                                                            int count = buffLength;
                                                            int numRead = 0;

                                                            do
                                                            {
                                                                numRead = sourceStream.Read(buff, offset, count);
                                                                zipStream.Write(buff, 0, numRead);
                                                            }
                                                            while (numRead == buffLength);
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception e)
                                            {
                                                m_Logger.Error("Failed to process attachment: " + sourceFile + ", " + e.Message);
                                                m_Logger.Error("Trace: " + e.StackTrace);
                                                // Skip to the next attachment
                                                continue;
                                            }

                                            byte[] fileBytes = File.ReadAllBytes(@targetFile);
                                            attachRecord.Content.Value = Convert.ToBase64String(fileBytes);
                                            attachRecord.Content.Encoding = GsaFeedContentEncoding.Base64Compressed;

                                            if ((attachRecord.Action == GsaFeedRecordAction.Add))
                                            {
                                                // Update the record url so it doesn't overwrite the parent, but leave the displayUrl as is
                                                attachRecord.Url = recordBaseUrl + "|" + sourceFileName;

                                                //We need to push the acl virtual groups
                                                PushGroupFromAcl(client.PermissionManager, s_Configuration.ProviderId, attachRecord.Acl);

                                                int parentIdx = attachRecord.Metadata.Values.FindIndex(item => item.Name == "uid");
                                                if (parentIdx >= 0)
                                                {
                                                    try
                                                    {
                                                        string parentId = attachRecord.Metadata.Values[parentIdx].Content;

                                                        client.DocumentManager.AddOrUpdateDocument(sourceId,
                                                            CreateDocumentFromRecord(attachRecord, header.FeedType == GsaFeedType.MetadataAndUrl, parentId, sourceFileExt),
                                                            null);
                                                        addedDocs++;
                                                        m_Logger.Info("Success: " + attachRecord.Action.ToString().ToUpperInvariant() + " " + filePath);
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        m_Logger.Error("Failed to add item: " + recordBaseUrl);
                                                        m_Logger.Error("Reason: " + e.Message);
                                                    }
                                                }
                                                else
                                                {
                                                    try
                                                    {
                                                        m_Logger.Warning("Metadata field \"uid\" missing in record. Attachment will not be bound to the parent.");
                                                        client.DocumentManager.AddOrUpdateDocument(sourceId,
                                                            CreateDocumentFromRecord(attachRecord, header.FeedType == GsaFeedType.MetadataAndUrl),
                                                            null);
                                                        addedDocs++;
                                                        m_Logger.Info("Success: " + attachRecord.Action.ToString().ToUpperInvariant() + " " + attachRecord.Url);
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        m_Logger.Error("Failed to add item: " + recordBaseUrl);
                                                        m_Logger.Error("Reason: " + e.Message);
                                                    }
                                                }
                                            }
                                        }
                                        // Remove this file from the temp space
                                        File.Delete(filePath);
                                    }

                                    // Remove this directory from the temp space
                                    if (Directory.GetFiles(dirPath).Length > 0)
                                    {
                                        m_Logger.Error("Attachment folder is not empty: " + dirPath);
                                    }
                                    else
                                    {
                                        Directory.Delete(dirPath);
                                    }
                                }
                                catch (Exception e)
                                {
                                    m_Logger.Error("Failed to read attachment: " + dirPath + ", " + e.Message);
                                    m_Logger.Error("Trace: " + e.StackTrace);
                                }
                            //} else {
                            //    m_Logger.Warning("Attachment folder does not exist: " + dirPath);
                            //}
                        }

                    }

                    if (header.FeedType == GsaFeedType.Full) {
                        m_Logger.Info("Full feed detected - Deleting old documents. Reference ordering Id: "+ firstOrderingIdRef);
                        client.DocumentManager.DeleteDocumentsOlderThan(sourceId, firstOrderingIdRef, 5);
                    }

                    try
                    {
                        client.ActivityManager.UpdateSourceStatus(sourceId, SourceStatusType.Idle);
                    }
                    catch (Exception e)
                    {
                    }

                    m_Logger.Info("The feed was processed.");
                    m_Logger.Info(" ");
                    m_Logger.Info("Statistics:");
                    m_Logger.Info("> Added documents: "+ addedDocs);
                    m_Logger.Info("> Deleted documents: "+ deletedDocs);
                    m_Logger.Info("> Ignored documents: "+ ignoredDocs);
                    m_Logger.Info(" ");

                    // The local XML files are no longer needed
                    var files = new DirectoryInfo(s_Configuration.TempFolder).GetFiles("*.*");
                    foreach (var file in files)
                    {
                        if (DateTime.UtcNow - file.CreationTimeUtc > TimeSpan.FromDays(10))
                        {
                            File.Delete(file.FullName);
                        }
                    }
                }
            
            } else {
                if (p_HttpRequest == null) {
                    m_Logger.Error("No HTTP request to process.");
                } else {
                    m_Logger.Error("Invalid received request: "+ p_HttpRequest.HttpMethod + " - "+ p_HttpRequest.Url);
                }
            }
        }

        private static PushDocument CreateDocumentFromRecord(GsaFeedRecord p_Record,
            bool p_DownloadContent)
        {
            return CreateDocumentFromRecord(p_Record, p_DownloadContent, null, null);
        }

        private static PushDocument CreateDocumentFromRecord(GsaFeedRecord p_Record,
            bool p_DownloadContent, string p_ParentId, string p_fileExt)
        {
            IDictionary<string, JToken> metadata = p_Record.ConvertMetadata();

            if (p_Record.DisplayUrl == null)
            {
                p_Record.DisplayUrl = p_Record.Url;
            }

            p_Record.Url = p_Record.Url.Replace("&", "|");

            metadata.Add("clickableuri", p_Record.DisplayUrl);
            metadata.Add(nameof(p_Record.DisplayUrl), p_Record.DisplayUrl);
            metadata.Add(nameof(p_Record.Lock), p_Record.Lock);
            metadata.Add(nameof(p_Record.MimeType), p_Record.MimeType);
            metadata.Add(nameof(p_Record.PageRank), p_Record.PageRank);
            metadata.Add(nameof(p_Record.Scoring), p_Record.Scoring);
            metadata.Add(nameof(p_Record.Url), p_Record.Url);
            metadata.Add(nameof(p_Record.AuthMethod), p_Record.AuthMethod.ToString());
            metadata.Add(nameof(p_Record.CrawlImmediately), p_Record.CrawlImmediately);
            metadata.Add(nameof(p_Record.CrawlOnce), p_Record.CrawlOnce);

            PushDocument document = new PushDocument(p_Record.Url) {
                ModifiedDate = p_Record.LastModified ?? DateTime.MinValue,
                Metadata = metadata,
                ParentId = p_ParentId,
                FileExtension = p_fileExt
            };

            if (p_Record.Acl != null) {
                DocumentPermissionSet currentDocSet = new DocumentPermissionSet();

                PermissionIdentity denyGroup = new PermissionIdentity(p_Record.Url + DISALLOW_GROUP, PermissionIdentityType.VirtualGroup);
                PermissionIdentity allowGroup = new PermissionIdentity(p_Record.Url + ALLOW_GROUP, PermissionIdentityType.VirtualGroup);
                currentDocSet.DeniedPermissions.Add(denyGroup);
                currentDocSet.AllowedPermissions.Add(allowGroup);
                DocumentPermissionLevel currentDocLevel = new DocumentPermissionLevel();
                currentDocLevel.PermissionSets.Add(currentDocSet);


                if (p_Record.Acl.ParentAcl != null) {
                    GsaFeedAcl currentAcl = p_Record.Acl;
                    List<DocumentPermissionLevel> allLevels = new List<DocumentPermissionLevel>();
                    allLevels.Add(currentDocLevel);
                    int currentLevelIndex = 0;

                    while (currentAcl.ParentAcl != null) {
                        GsaFeedAcl curParentAcl = currentAcl.ParentAcl;
                        DocumentPermissionSet curParentDocSet = new DocumentPermissionSet();
                        PermissionIdentity parentDenyGroup = new PermissionIdentity(curParentAcl.DocumentUrl + DISALLOW_GROUP, PermissionIdentityType.VirtualGroup);
                        PermissionIdentity parentAllowGroup = new PermissionIdentity(curParentAcl.DocumentUrl + ALLOW_GROUP, PermissionIdentityType.VirtualGroup);


                        //We sill always need the parents in a different set
                        curParentDocSet.DeniedPermissions.Add(parentDenyGroup);
                        curParentDocSet.AllowedPermissions.Add(parentAllowGroup);
                        switch (curParentAcl.InheritanceType) {
                            case GsaFeedAclInheritance.BothPermit:
                                //The parent and the document are in two different sets

                                allLevels.ElementAt(currentLevelIndex).PermissionSets.Add(curParentDocSet);
                                break;
                            case GsaFeedAclInheritance.ChildOverrides:
                                //The parent is in a lower level than the current document
                                DocumentPermissionLevel parentLowerDocLevel = new DocumentPermissionLevel();
                                parentLowerDocLevel.PermissionSets.Add(curParentDocSet);
                                //We are adding our self after the children
                                currentLevelIndex++;
                                allLevels.Insert(currentLevelIndex, parentLowerDocLevel);
                                break;
                            case GsaFeedAclInheritance.ParentOverrides:
                                //The parent is in a higher level than the current document
                                //on doit ajouter avant l'enfant
                                DocumentPermissionLevel parentHigherDocLevel = new DocumentPermissionLevel();
                                parentHigherDocLevel.PermissionSets.Add(curParentDocSet);
                                allLevels.Insert(currentLevelIndex, parentHigherDocLevel);
                                break;
                            case GsaFeedAclInheritance.LeafNode:
                                //The document is not suppose to have inheritance from a leaf node
                                ConsoleUtilities.WriteLine("> Warning: You are trying to have inheritance on a LeafNode. Document in error: {0}", ConsoleColor.Yellow, p_Record.Url);
                                curParentAcl.ParentAcl = null;
                                break;
                        }
                        currentAcl = curParentAcl;
                    }
                    //Now we push the permissions
                    foreach (DocumentPermissionLevel documentPermissionLevel in allLevels) {
                        document.Permissions.Add(documentPermissionLevel);
                    }
                } else {
                    //We might need to add the parent level before, so we will not default this action.
                    document.Permissions.Add(currentDocLevel);
                }
            }

            if (p_DownloadContent) {
                string content = s_HttpDownloader.Download(p_Record.Url);

                PushDocumentHelper.SetCompressedEncodedContent(document, Compression.GetCompressedBinaryData(content));
            } else {
                if (p_Record.Content.Encoding == GsaFeedContentEncoding.Base64Compressed) {
                    PushDocumentHelper.SetCompressedEncodedContent(document, p_Record.Content.Value.Trim(Convert.ToChar("\n")));
                } else {
                    PushDocumentHelper.SetContent(document, p_Record.Content.GetDecodedValue());
                }
            }

            return document;
        }

        /// <summary>
        /// Reload the configuration from the file and validate that configuration is complete.
        /// </summary>
        /// <returns>Return if the configuration file is valid.</returns>
        private static bool UpdateAndValidateConfig()
        {
            bool isValid = true;
            string stringConfig = File.ReadAllText(m_ConfigFilePath);
            try
            {
                s_Configuration = (Configuration)JsonConvert.DeserializeObject(stringConfig, typeof(Configuration));
                List<string> missingConfig = s_Configuration.ValidateConfig();
                if (missingConfig.Count > 0)
                {
                    isValid = false;
                    foreach (string configName in missingConfig)
                    {
                        m_Logger.Error("The parameter " + configName + " is missing in the configuration file. Current path: " + m_ConfigFilePath);
                    }
                }
            }
            catch (Exception e)
            {
                m_Logger.Fatal("Not able to read JSON configuration object. Exiting");
                isValid = false;
            }
            return isValid;
        }

        private static void PushGroupFromAcl(IPermissionServiceManager p_PermissionPushManager,
            string p_ProviderId,
            GsaFeedAcl acl)
        {
            if (acl == null)
                return;

            PermissionIdentity denyIdentity = new PermissionIdentity(acl.DocumentUrl + DISALLOW_GROUP, PermissionIdentityType.VirtualGroup);
            PermissionIdentity allowIdentity = new PermissionIdentity(acl.DocumentUrl + ALLOW_GROUP, PermissionIdentityType.VirtualGroup);


            PermissionIdentityBody denyBody = new PermissionIdentityBody(denyIdentity);
            PermissionIdentityBody allowBody = new PermissionIdentityBody(allowIdentity);

            foreach (GsaFeedPrincipal principal in acl.Principals) {
                //We create the groups of the document based on the principals elements
                PermissionIdentity permission = new PermissionIdentity(principal.Value,
                    principal.AclScope == GsaFeedAclScope.Group ? PermissionIdentityType.Group : PermissionIdentityType.User);
                if (principal.Access == GsaFeedAclAccess.Permit) {
                    allowBody.Mappings.Add(permission);
                } else {
                    denyBody.Mappings.Add(permission);
                }
            }

            p_PermissionPushManager.AddOrUpdateIdentity(p_ProviderId, null, allowBody);
            p_PermissionPushManager.AddOrUpdateIdentity(p_ProviderId, null, denyBody);
        }

        private static void PushMemberOfGroup(IPermissionServiceManager p_PermissionServiceManager,
            string p_ProviderId,
            GsaFeedMembership p_Membership)
        {
            //TODO push les groupes
        }

        private static int search(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                if (match(haystack, needle, i))
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool match(byte[] haystack, byte[] needle, int start)
        {
            if (needle.Length + start > haystack.Length)
            {
                return false;
            }
            else
            {
                for (int i = 0; i < needle.Length; i++)
                {
                    if (needle[i] != haystack[i + start])
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private static byte[] cleanXmlBytes(byte[] xmlBuffer, int offset, int bytesToProcess)
        {
            for (var i = offset; i < bytesToProcess; i++)
            {
                byte current = xmlBuffer[i];

                if (
                    (current == 0x9) ||
                    (current == 0xA) ||
                    (current == 0xD) ||
                    ((current >= 0x20) && (current <= 0xD7FF)) ||
                    ((current >= 0xE000) && (current <= 0xFFFD)))
                {
                    // Nothing to do, skip to the next byte
                } else
                {
                    // Bad character found, switch it to a space (0x20)
                    xmlBuffer[i] = 0x20;
                }
            }

            return xmlBuffer;
        }

    }

    public class G2CoveoResponse
    {
        public HttpStatusCode statusCode { get; set; }
        public string reason { get; set; }
    }
}
