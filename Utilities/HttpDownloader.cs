// Copyright (c) 2005-2016, Coveo Solutions Inc.

using System;
using System.Threading;
using Coveo.Connectors.Utilities.PushApiSdk.Response;
using Coveo.Connectors.Utilities.Rest.Http;
using Coveo.Connectors.Utilities.Rest.Request;

namespace GSAFeedPushConverter.Utilities
{
    public class HttpDownloader : IDisposable
    {
        private readonly IHttpRequestManager m_HttpRequestManager;
        public HttpDownloader()
        {
            m_HttpRequestManager = new HttpRequestManager(new HttpClientWrapper(120), new HttpResponseParser());
        }

        public string Download(string p_Url)
        {
            return m_HttpRequestManager.Execute<string, string>(new RestRequest(new Uri(p_Url)), CancellationToken.None).ResponseContent;
        }

        public void Dispose()
        {
            m_HttpRequestManager?.Dispose();
        }
    }
}
