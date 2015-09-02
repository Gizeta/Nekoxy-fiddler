using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TrotiNet;
using Fiddler;

namespace Nekoxy
{
    /// <summary>
    /// HTTPプロキシサーバー。
    /// HTTPプロトコルにのみ対応し、HTTPS等はサポートしない。
    /// </summary>
    public static class HttpProxy
    {
        /// <summary>
        /// HTTPレスポンスをプロキシ クライアントに送信完了した際に発生。
        /// </summary>
        public static event Action<Session> AfterSessionComplete;

        /// <summary>
        /// リクエストヘッダを読み込み完了した際に発生。
        /// ボディは受信前。
        /// </summary>
        public static event Action<HttpRequest> AfterReadRequestHeaders;

        /// <summary>
        /// レスポンスヘッダを読み込み完了した際に発生。
        /// ボディは受信前。
        /// </summary>
        public static event Action<HttpResponse> AfterReadResponseHeaders;

        /// <summary>
        /// 上流プロキシ設定。
        /// </summary>
        public static ProxyConfig UpstreamProxyConfig
        {
            get;
            set;
        }

        /// <summary>
        /// プロキシサーバーが Listening 中かどうかを取得。
        /// </summary>
        public static bool IsInListening => FiddlerApplication.IsStarted();

        /// <summary>
        /// 指定ポートで Listening を開始する。
        /// Shutdown() を呼び出さずに2回目の Startup() を呼び出した場合、InvalidOperationException が発生する。
        /// </summary>
        /// <param name="listeningPort">Listeningするポート。</param>
        /// <param name="useIpV6">falseの場合、127.0.0.1で待ち受ける。trueの場合、::1で待ち受ける。既定false。</param>
        /// <param name="isSetProxyInProcess">trueの場合、プロセス内IEプロキシの設定を実施し、HTTP通信をNekoxyに向ける。既定true。</param>
        public static void Startup(int listeningPort, bool useIpV6 = false, bool isSetProxyInProcess = true)
        {
            if (IsInListening) throw new InvalidOperationException("Calling Startup() twice without calling Shutdown() is not permitted.");

            FiddlerApplication.BeforeRequest += setUpstreamProxyHandler;
            FiddlerApplication.AfterSessionComplete += raiseAfterSessionComplete;
            FiddlerApplication.RequestHeadersAvailable += raiseRequestHeadersAvailable;
            FiddlerApplication.ResponseHeadersAvailable += raiseResponseHeadersAvailable;

            ListeningPort = listeningPort;
            try
            {
                if (isSetProxyInProcess)
                    WinInetUtil.SetProxyInProcessForNekoxy(listeningPort);

                FiddlerApplication.Startup(listeningPort, FiddlerCoreStartupFlags.ChainToUpstreamGateway);
            }
            catch (Exception)
            {
                Shutdown();
                throw;
            }
        }

        /// <summary>
        /// Listening しているスレッドを終了し、ソケットを閉じる。
        /// </summary>
        public static void Shutdown()
        {
            FiddlerApplication.BeforeRequest -= setUpstreamProxyHandler;
            FiddlerApplication.AfterSessionComplete -= raiseAfterSessionComplete;
            FiddlerApplication.RequestHeadersAvailable -= raiseRequestHeadersAvailable;
            FiddlerApplication.ResponseHeadersAvailable -= raiseResponseHeadersAvailable;

            FiddlerApplication.Shutdown();
        }

        internal static int ListeningPort { get; set; }

        private static void InvokeAfterSessionComplete(Session session)
            => AfterSessionComplete?.Invoke(session);

        private static void InvokeAfterReadRequestHeaders(HttpRequest request)
            => AfterReadRequestHeaders?.Invoke(request);

        private static void InvokeAfterReadResponseHeaders(HttpResponse response)
            => AfterReadResponseHeaders?.Invoke(response);

        private static void setUpstreamProxyHandler(Fiddler.Session requestingSession)
        {
            if (UpstreamProxyConfig.Type == ProxyConfigType.DirectAccess) return;

            string host = "";
            int port = 0;
            if (UpstreamProxyConfig.Type == ProxyConfigType.SpecificProxy)
            {
                host = UpstreamProxyConfig.SpecificProxyHost;
                port = UpstreamProxyConfig.SpecificProxyPort;
            }
            else
            {
                host = requestingSession.isHTTPS || requestingSession.port == 443 ? WinInetUtil.GetSystemHttpsProxyHost() : WinInetUtil.GetSystemHttpProxyHost();
                port = requestingSession.isHTTPS || requestingSession.port == 443 ? WinInetUtil.GetSystemHttpsProxyPort() : WinInetUtil.GetSystemHttpProxyPort();

                if (port == ListeningPort && host.IsLoopbackHost())
                    return;
            }

            if (string.IsNullOrEmpty(host))
                return;

            var gateway = host.Contains(":") ? string.Format("[{0}]:{1}", host, port) : string.Format("{0}:{1}", host, port);

            requestingSession["X-OverrideGateway"] = gateway;
        }

        private static void raiseAfterSessionComplete(Fiddler.Session session)
        {
            InvokeAfterSessionComplete(session.ToNekoxySession());
        }

        private static void raiseRequestHeadersAvailable(Fiddler.Session session)
        {
            InvokeAfterReadRequestHeaders(new HttpRequest(session.GenerateRequestLine(), session.RequestHeaders.GenerateHeaders(), null));
        }

        private static void raiseResponseHeadersAvailable(Fiddler.Session session)
        {
            InvokeAfterReadResponseHeaders(new HttpResponse(session.GenerateStatusLine(), session.ResponseHeaders.GenerateHeaders(), null));
        }
    }
}
