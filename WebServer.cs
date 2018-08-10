// Copyright (c) 2005-2016, Coveo Solutions Inc.

using System;
using System.Net;
using System.Text;
using System.Threading;
using Coveo.Connectors.Utilities;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Collections.Specialized;

namespace GSAFeedPushConverter
{
    public class WebServer
    {
        private readonly HttpListener m_Listener = new HttpListener();
        private readonly Func<HttpListenerRequest, FeedRequestResponse> m_ResponderMethod;
        private readonly Action<HttpListenerRequest> m_ProcessMethod;

        public WebServer(string[] p_Prefixes,
            Func<HttpListenerRequest, FeedRequestResponse> p_ResponderMethod,
            Action<HttpListenerRequest> p_ProcessMethod)
        {
            if (!HttpListener.IsSupported) {
                throw new NotSupportedException("Http Listener is unsupported on this machine.");
            }

            if (p_Prefixes == null || p_Prefixes.Length == 0) {
                throw new ArgumentException(nameof(p_Prefixes));
            }

            if (p_ResponderMethod == null) {
                throw new ArgumentException(nameof(p_ResponderMethod));
            }

            if (p_ProcessMethod == null) {
                throw new ArgumentException(nameof(p_ProcessMethod));
            }

            foreach (string prefix in p_Prefixes) {
                m_Listener.Prefixes.Add(prefix);
            }

            m_ResponderMethod = p_ResponderMethod;
            m_ProcessMethod = p_ProcessMethod;
            m_Listener.Start();
        }

        public WebServer(Func<HttpListenerRequest, FeedRequestResponse> p_ResponderMethod,
            Action<HttpListenerRequest> p_ProcessMethod,
            params string[] p_Prefixes)
            : this(p_Prefixes, p_ResponderMethod, p_ProcessMethod)
        {
        }

        public void Run()
        {
            ThreadPool.QueueUserWorkItem(o => {
                Console.WriteLine("Registering...");
                m_Listener.Prefixes.ForEach(prefix => Console.WriteLine("> {0}", prefix));
                Console.WriteLine();
                ConsoleUtilities.WriteLine("Web server is listening...", ConsoleColor.Green);
                Console.WriteLine();

                while (m_Listener.IsListening) {
                    ThreadPool.QueueUserWorkItem(listenerContext =>
                    {
                        HttpListenerContext context = listenerContext as HttpListenerContext;
                        try
                        {
                            if (context != null)
                            {
                                String q_wait = context.Request.QueryString.Get("waittocomplete");

                                bool willWaitToComplete = ((q_wait != null) && ("yes".Equals(q_wait.ToLower()))) ? true : false;
                                string feedFilePath = Program.LoadFeed(context.Request);

                                if (feedFilePath != null)
                                {
                                    // XML was successfully loaded
                                    context.Request.Headers.Set("FeedFilePath", feedFilePath);

                                    if (! willWaitToComplete)
                                    {
                                        // This request doesn't want to wait, so respond and then we'll get started
                                        FeedRequestResponse response = new FeedRequestResponse();
                                        response.HttpStatusCode = HttpStatusCode.OK;
                                        response.Message = "GSA Feed loaded successfully";

                                        context.Response.StatusCode = (int)response.HttpStatusCode;
                                        byte[] msgBuf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response, Formatting.Indented));
                                        context.Response.ContentType = "application/json";
                                        context.Response.ContentLength64 = msgBuf.Length;
                                        context.Response.OutputStream.Write(msgBuf, 0, msgBuf.Length);
                                        context.Response.OutputStream.Close();
                                    }

                                    // Begin processing the feed records ... this could take a while
                                    m_ProcessMethod(context.Request);

                                    if (willWaitToComplete)
                                    {
                                        // Since the requestor wanted to wait, send them their status
                                        FeedRequestResponse response = m_ResponderMethod(context.Request);
                                        context.Response.StatusCode = (int)response.HttpStatusCode;
                                        byte[] buf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response, Formatting.Indented));
                                        context.Response.ContentType = "application/json";
                                        context.Response.ContentLength64 = buf.Length;
                                        context.Response.OutputStream.Write(buf, 0, buf.Length);

                                        if (response.HttpStatusCode != HttpStatusCode.OK)
                                        {
                                            Program.m_Logger.Error("Error response: " + response.HttpStatusCode + " - " + response.Message);
                                        }
                                    }
                                } else {
                                    // Something went wrong loading the XML feed
                                    FeedRequestResponse response = new FeedRequestResponse();
                                    response.HttpStatusCode = HttpStatusCode.BadRequest;
                                    response.Message = "GSA Feed failed to load. Check the logs for details.";

                                    context.Response.StatusCode = (int)response.HttpStatusCode;
                                    byte[] msgBuf = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response, Formatting.Indented));
                                    context.Response.ContentType = "application/json";
                                    context.Response.ContentLength64 = msgBuf.Length;
                                    context.Response.OutputStream.Write(msgBuf, 0, msgBuf.Length);
                                    context.Response.OutputStream.Close();

                                }

                                Console.WriteLine();
                                ConsoleUtilities.WriteLine("Web server is listening...", ConsoleColor.Green);
                                Console.WriteLine();
                            }
                        }
                        catch (System.ObjectDisposedException e)
                        {

                        }
                        finally
                        {
                            try
                            {
                                context.Response.OutputStream.Close();
                            } catch
                            {
                                // No need to do anything, just closing the stream just in case it was still open
                            }
                        }
                    }, m_Listener.GetContext());
                }
            });
        }

        public void Stop()
        {
            m_Listener.Stop();
            m_Listener.Close();
        }
    }
}
