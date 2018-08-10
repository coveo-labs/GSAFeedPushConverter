// Copyright (c) 2005-2016, Coveo Solutions Inc.

using System.Net;
using Newtonsoft.Json;

namespace GSAFeedPushConverter
{
    public class FeedRequestResponse
    {
        [JsonProperty(PropertyName = "httpStatusCode")]
        public HttpStatusCode HttpStatusCode { get; set; }

        [JsonProperty(PropertyName = "httpStatusDescription")]
        public string HttpStatusDescription => HttpStatusCode.ToString();

        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }
    }
}
