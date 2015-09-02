using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Nekoxy
{
    internal class SocketMocker
    {
        private static Socket serverSocket;
        private static Socket clientRequestSocket;
        private static Socket clientResponseSocket;
        private static bool useIpV6;
        private static byte[] buffer = new byte[512];
        private static bool isRunning = false;

        public static Socket GetFakeRequestSocket()
        {
            clientRequestSocket.Send(Encoding.ASCII.GetBytes("request"));
            return clientRequestSocket;
        }

        public static Socket GetFakeResponseSocket()
        {
            clientResponseSocket.Send(Encoding.ASCII.GetBytes("response"));
            return clientResponseSocket;
        }

        public static void Startup(bool useIpV6 = false)
        {
            isRunning = true;

            SocketMocker.useIpV6 = useIpV6;
            serverSocket = new Socket(useIpV6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(useIpV6 ? IPAddress.IPv6Loopback : IPAddress.Loopback, 46984));
            serverSocket.Listen(10);
            serverSocket.BeginAccept(new AsyncCallback(clientAccepted), serverSocket);

            clientRequestSocket = new Socket(useIpV6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientRequestSocket.Connect(new IPEndPoint(useIpV6 ? IPAddress.IPv6Loopback : IPAddress.Loopback, 46984));
            clientResponseSocket = new Socket(useIpV6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientResponseSocket.Connect(new IPEndPoint(useIpV6 ? IPAddress.IPv6Loopback : IPAddress.Loopback, 46984));
        }

        public static void Shutdown()
        {
            isRunning = false;

            serverSocket.Dispose();
            clientRequestSocket.Dispose();
            clientResponseSocket.Dispose();
        }

        private static void clientAccepted(IAsyncResult ar)
        {
            var server = ar.AsyncState as Socket;
            if (!isRunning) return;
            var client = server.EndAccept(ar);

            client.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(receiveMessage), client);
            server.BeginAccept(new AsyncCallback(clientAccepted), server);
        }

        private static void receiveMessage(IAsyncResult ar)
        {
            var socket = ar.AsyncState as Socket;
            var length = socket.EndReceive(ar);
            var message = Encoding.ASCII.GetString(buffer, 0, length);
            if (message == "request")
            {
                socket.Send(Encoding.ASCII.GetBytes("GET http://www.dmm.com/ HTTP/1.1\r\nGET http://www.dmm.com/ HTTP/1.1\r\n"));
            }
            else if (message == "response")
            {
                socket.Send(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nHTTP/1.1 200 OK\r\n"));
            }

            socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(receiveMessage), socket);
        }
    }
}
