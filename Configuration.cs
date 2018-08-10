// Copyright (c) 2005-2016, Coveo Solutions Inc.

using Newtonsoft.Json;
using System.Collections.Generic;
using System.Reflection;

namespace GSAFeedPushConverter
{
    public class Configuration
    {

        [JsonProperty(PropertyName = "PLATFORM_API_ENDPOINT")]
        public string PlatformApiEndpointUrl { get; set; }

        [JsonProperty(PropertyName = "PUSH_API_ENDPOINT")]
        public string PushApiEndpointUrl { get; set; }

        [JsonProperty(PropertyName = "ORGANIZATION_ID")]
        public string OrganizationId { get; set; }

        [JsonProperty(PropertyName = "PROVIDER_ID")]
        public string ProviderId { get; set; }

        [JsonProperty(PropertyName = "API_KEY")]
        public string ApiKey { get; set; }

        [JsonProperty(PropertyName = "PUSH_SOURCE_ID")]
        public string SourceId { get; set; }

        [JsonProperty(PropertyName = "PUSH_SOURCE_DICTIONNARY")]
        public Dictionary<string, string> DataSourceToSourceId { get; set; } = new Dictionary<string, string>();

        [JsonProperty(PropertyName = "LISTENING_HOST")]
        public string ListeningHost { get; set; }

        [JsonProperty(PropertyName = "LISTENING_PORT")]
        public string ListeningPort { get; set; }

        [JsonProperty(PropertyName = "TEMP_FOLDER")]
        public string TempFolder { get; set; }

        /*
         [JsonProperty(PropertyName = "LISTENING_URL_FEED")]
         public string ListeningUrlFeed { get; set; } = LISTENING_URL_FEED;

         [JsonProperty(PropertyName = "LISTENING_URL_GROUPS")]
         public string ListeningUrlGroups { get; set; } = LISTENING_URL_GROUPS;

         [JsonProperty(PropertyName = "GSA_AUTH_URL")]
         public string GsaMockAuth { get; set; } = GSA_AUTH_URL;
         */

        [JsonProperty(PropertyName = "DELETE_ON_INVALID_URL")]
        public bool DeleteOnInvalidUrl { get; set; }

        [JsonProperty(PropertyName = "REQUIRE_DISPLAY_URL")]
        public bool RequireDisplayUrl { get; set; }

        //This option has not been tested. The tested connectors always had ACL with the records.
        [JsonProperty(PropertyName = "PUSH_RECORDS_WITHOUT_ACL")]
        public bool PushRecordsWithoutAcl { get; set; }

        /// <summary>
        /// Return the list of missing parameters.
        /// </summary>
        /// <returns>List of missing parameters.</returns>
        public List<string> ValidateConfig()
        {
            List<string> missingParameters = new List<string>();
            foreach (PropertyInfo propertyInfo in this.GetType().GetProperties())
            {
                if (propertyInfo.PropertyType == typeof(string))
                {
                    if (string.IsNullOrEmpty((string)propertyInfo.GetValue(this)))
                    {
                        missingParameters.Add(propertyInfo.Name);
                    }
                }
            }

            return missingParameters;
        }

    }
}
