using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MultiServer
{
    class MultiServer
    {
        private static readonly Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static readonly List<Socket> clientSockets = new List<Socket>();
        private const int BUFFER_SIZE = 2048;
        private const int PORT = 8888;
        private static readonly byte[] buffer = new byte[BUFFER_SIZE];

        static void Main()
        {
            Console.Title = "Server";
            SetupServer();
            Console.ReadLine(); // When we press enter close everything
            CloseAllSockets();
        }
    
        private static void SetupServer()
        {
            Console.WriteLine("Setting up server...");
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, PORT));
            serverSocket.Listen(0);
            serverSocket.BeginAccept(AcceptCallback, null);
            Console.WriteLine("Server setup complete");
        }

        /// <summary>
        /// Close all connected client (we do not need to shutdown the server socket as its connections
        /// are already closed with the clients).
        /// </summary>
        private static void CloseAllSockets()
        {
            foreach (Socket socket in clientSockets)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            serverSocket.Close();
        }

        private static void AcceptCallback(IAsyncResult AR)
        {
            Socket socket;
            try
            {
                socket = serverSocket.EndAccept(AR);
            }
            catch (ObjectDisposedException) // I cannot seem to avoid this (on exit when properly closing sockets)
            {
                return;
            }

            clientSockets.Add(socket);
            socket.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, socket);
            Console.WriteLine("Client connected, waiting for request...");
            Console.WriteLine("---------------------------------------------------------------------");
            serverSocket.BeginAccept(AcceptCallback, null);
        }

        private static void ReceiveCallback(IAsyncResult AR)
        {
            Socket current = (Socket)AR.AsyncState;
            int received;

            try
            {
                received = current.EndReceive(AR);
            }
            catch (SocketException)
            {
                Console.WriteLine("Client forcefully disconnected");
                Console.WriteLine("---------------------------------------------------------------------");
                // Don't shutdown because the socket may be disposed and its disconnected anyway.
                current.Close(); 
                clientSockets.Remove(current);
                return;
            }

            byte[] recBuf = new byte[received];
            Array.Copy(buffer, recBuf, received);
            string text = Encoding.ASCII.GetString(recBuf);
            RequestFilter(text, current);
   
            Console.WriteLine("Received Text: " + text);
            Console.WriteLine("---------------------------------------------------------------------");

            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
        }

        public static void RequestFilter(string text, Socket current)
        {
            if (text.ToLower().Contains("tell") && text.ToLower().Contains("time") || text.ToLower().Contains("get") && text.ToLower().Contains("time") || text.ToLower().Contains("give") && text.ToLower().Contains("time")) // Client requested time
            {
                try
                {
                    Console.WriteLine("Text is a GET TIME request");
                    byte[] data = Encoding.ASCII.GetBytes(DateTime.Now.ToLongTimeString());
                    current.Send(data);
                    Console.WriteLine("Time sent to client");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            else if (text.ToLower().Contains("tell") && text.ToLower().Contains("date"))
            {
                try
                {
                    Console.WriteLine("Text is a GET DATE request");
                    byte[] data = Encoding.ASCII.GetBytes(DateTimeOffset.Now.ToString());
                    current.Send(data);
                    Console.WriteLine("Date sent to the client");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            else if (text.ToLower() == "exit") // Client wants to exit gracefully
            {
                // Always Shutdown before closing
                try
                {
                    current.Shutdown(SocketShutdown.Both);
                    current.Close();
                    clientSockets.Remove(current);
                    Console.WriteLine("Client disconnected");
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            else if (text.ToLower().Contains("add"))
            {
                try
                {
                    string[] parts = text.Split(' '); // split by space
                    if (parts.Length != 3)
                    {
                        byte[] data0 = Encoding.ASCII.GetBytes("Illegal add request");
                        current.Send(data0);
                    }
                    var operation = parts[0];
                    var numberStr1 = parts[1];
                    string numberStr2 = parts[2];

                    int addResult = Convert.ToInt16(numberStr1) + Convert.ToInt16(numberStr2);

                    Console.WriteLine("Text is ADD request");
                    byte[] data = Encoding.ASCII.GetBytes($"Number1 {numberStr1} and Number2 {numberStr2} has been added. Result is {addResult}");
                    current.Send(data);
                    Console.WriteLine("Numbers are added, and the result has been sent to the client.");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            else if (text.ToLower().Contains("how") && text.ToLower().Contains("are") && text.ToLower().Contains("you"))
            {
                try
                {
                    Random r = new Random();
                    byte[] data;

                    switch (r.Next(0, 2))
                    {
                        case 0:
                            data = Encoding.ASCII.GetBytes("I feel pretty good today thank you.");
                            current.Send(data);
                            break;

                        case 1:
                            data = Encoding.ASCII.GetBytes("I felt better before , but im fine thanks.");
                            current.Send(data);
                            break;
                        case 2:
                            data = Encoding.ASCII.GetBytes("Well...I have shitty day. To many request I get.. Please shut me down.");
                            current.Send(data);
                            break;
                    }

                    Console.WriteLine("Text is a HOW ARE YOU request");
                    Console.WriteLine("Answer sent to the client");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            else if (text.ToLower().Contains("number of socket"))
            {
                try
                {
                    Console.WriteLine("Text is a SOCKET NUM request");
                    byte[] data = Encoding.ASCII.GetBytes(clientSockets.Count.ToString());
                   // current.Send(data);
                    foreach (var socket in clientSockets)
                    {
                        socket.Send(data);
                    }
                    Console.WriteLine("Number of sockets has been sent to the client");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            else
            {
                Console.WriteLine("Text is normal message not a request");
                byte[] data = Encoding.ASCII.GetBytes("Server has got your message!");
                current.Send(data);
            }
        }
    }
}
