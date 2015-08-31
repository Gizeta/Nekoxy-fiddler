using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TrotiNet;
using Fiddler;

namespace Nekoxy
{
    internal static class Extensions
    {
        public static Encoding GetEncoding(this HttpHeaders headers)
        {
            if (!headers.Headers.ContainsKey("content-type")) return defaultEncoding;
            var match = charsetRegex.Match(headers.Headers["content-type"]);
            if (!match.Success) return defaultEncoding;
            try
            {
                return Encoding.GetEncoding(match.Groups[1].Value);
            }
            catch
            {
                return defaultEncoding;
            }
        }

        public static string GetMimeType(this string contentType)
        {
            var match = mimeTypeRegex.Match(contentType);
            return match.Success
                ? match.Groups[1].Value
                : string.Empty;
        }

        public static bool IsLoopbackHost(this string hostName)
        {
            var localAddresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
            IPAddress parsed;
            if (IPAddress.TryParse(hostName, out parsed))
                return IPAddress.IsLoopback(parsed) || localAddresses.Any(x => x.Equals(parsed));

            var addresses = Dns.GetHostEntry(hostName).AddressList;
            return addresses.Any(IPAddress.IsLoopback) || addresses.Intersect(localAddresses).Any();
        }

        public static bool IsOwnProxy(this Uri proxy)
        {
            return proxy.Port == HttpProxy.ListeningPort
                   && proxy.Host.IsLoopbackHost();
        }

        public static bool IsUnknownLength(this HttpHeaders responseHeaders)
        {
            var isChunked = responseHeaders.TransferEncoding?.Contains("chunked") ?? false;
            return !isChunked && responseHeaders.ContentLength == null;
        }

        public static string ToString(this byte[] bytes, Encoding charset)
        {
            return charset.GetString(bytes);
        }

        private static readonly Encoding defaultEncoding = Encoding.ASCII;
        private static readonly Regex charsetRegex = new Regex("charset=([\\w-]*)", RegexOptions.Compiled);
        private static readonly Regex mimeTypeRegex = new Regex("^([^;]+)", RegexOptions.Compiled);

        public static HttpRequestLine GenerateRequestLine(this Fiddler.Session session)
        {
            var constructor = typeof(HttpRequestLine).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(HttpSocket) }, null);
            var instance = (HttpRequestLine)constructor.Invoke(new object[] { new HttpSocket(SocketMocker.GetFakeRequestSocket()) });

            instance.Method = session.RequestMethod;
            instance.ProtocolVersion = session.RequestHeaders.HTTPVersion.Replace("HTTP/", "");
            instance.URI = session.PathAndQuery;

            return instance;
        }

        public static HttpStatusLine GenerateStatusLine(this Fiddler.Session session)
        {
            var constructor = typeof(HttpStatusLine).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(HttpSocket) }, null);
            var instance = (HttpStatusLine)constructor.Invoke(new object[] { new HttpSocket(SocketMocker.GetFakeResponseSocket()) });

            typeof(HttpStatusLine).GetProperty("ProtocolVersion").SetValue(instance, session.ResponseHeaders.HTTPVersion.Replace("HTTP/", ""));
            typeof(HttpStatusLine).GetProperty("StatusCode").SetValue(instance, session.responseCode);
            typeof(HttpStatusLine).GetField("StatusLine").SetValue(instance, session.ResponseHeaders.HTTPVersion + " " + session.ResponseHeaders.HTTPResponseStatus);

            return instance;
        }

        public static HttpHeaders GenerateHeaders(this Fiddler.HTTPHeaders httpHeaders)
        {
            var headers = new HttpHeaders();

            StringBuilder sb = new StringBuilder();

            foreach (HTTPHeaderItem pair in httpHeaders)
            {
                if (pair.Name.Equals("connection") ||
                    pair.Name.Equals("content-encoding") ||
                    pair.Name.Equals("proxy-connection") ||
                    pair.Name.Equals("transfer-encoding"))
                {
                    headers.Headers[pair.Name.ToLower()] = pair.Value.ToLower();
                }
                else
                {
                    headers.Headers[pair.Name.ToLower()] = pair.Value;
                }
                sb.Append(pair.Name + ": " + pair.Value + "\r\n");
            }

            typeof(HttpHeaders).GetProperty("HeadersInOrder").SetValue(headers, sb.ToString());

            headers.Connection = headers.Headers.ContainsKey("connection") ? new string[] { headers.Headers["connection"] } : null;
            headers.ContentEncoding = headers.Headers.ContainsKey("content-encoding") ? headers.Headers["content-encoding"] : null;
            uint length;
            if (headers.Headers.ContainsKey("content-length") && uint.TryParse(headers.Headers["content-length"], out length))
            {
                headers.ContentLength = length;
            }
            else
            {
                headers.ContentLength = null;
            }
            headers.Host = headers.Headers.ContainsKey("host") ? headers.Headers["host"] : null;
            headers.ProxyConnection = headers.Headers.ContainsKey("proxy-connection") ? new string[] { headers.Headers["proxy-connection"] } : null;
            headers.Referer = headers.Headers.ContainsKey("referer") ? headers.Headers["referer"] : null;
            headers.TransferEncoding = headers.Headers.ContainsKey("transfer-encoding") ? new string[] { headers.Headers["transfer-encoding"] } : null;

            return headers;
        }
    }
}
