using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

class Server
{
    static void Main(string[] args)
    {
        IPAddress ip = IPAddress.Parse("192.168.1.37");
        // create a TCP/IP socket
        Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Console.WriteLine("Server ip is 192.168.1.37");
        // bind the socket to a local endpoint and start listening
        listener.Bind(new IPEndPoint(ip, 8888));
        listener.Listen(10);

        Console.WriteLine("Server started. Listening for incoming connections...");
        List<ClientInfo> _clients = new List<ClientInfo>();

        while (true)
        {
            // accept incoming connections
            Socket handler = listener.Accept();
            Console.WriteLine("Client connected: {0}", handler.RemoteEndPoint.ToString());
            // start a new thread to handle the client
            ClientHandler clientHandler = new ClientHandler(handler, ref _clients);
        }
    }
}

class ClientHandler
{
    private Socket _socket;
    static List<ClientInfo> _clients;

    public ClientHandler(Socket socket, ref List<ClientInfo> clients)
    {
        _socket = socket;
        _clients = clients;

        // start a new thread to handle the client
        new System.Threading.Thread(() =>
        {
            // authenticate the client
            bool isAuthenticated = false;
            string clientId = "";
            while (!isAuthenticated)
            {
                // receive request from the client
                byte[] buffer = new byte[1024];
                int bytesRead = _socket.Receive(buffer);
                string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                Console.WriteLine("Received request from {0}: {1}", _socket.RemoteEndPoint.ToString(), message);

                // parse the request and handle it according to the protocol
                string[] parts = message.Split(':');

                if (parts.Length < 3)
                {
                    // invalid request format
                    byte[] response = Encoding.ASCII.GetBytes("invalid request format");
                    _socket.Send(response);
                    continue;
                }

                string requestType = parts[0];
                string username = parts[1];
                string password = parts[2];

                switch (requestType)
                {
                    case "login":
                        if (IsValidCredentials(username, password, false))
                        {
                            isAuthenticated = true;
                            clientId = username;
                            byte[] responseBuffer = Encoding.ASCII.GetBytes("login_ok");
                            _socket.Send(responseBuffer);
                        }
                        else
                        {
                            byte[] responseBuffer = Encoding.ASCII.GetBytes("invalid username or password");
                            _socket.Send(responseBuffer);
                        }
                        break;

                    case "signup":
                        bool isSignUpSuccessful = IsValidCredentials(username, password, true);

                        if (isSignUpSuccessful)
                        {
                            byte[] responseBuffer = Encoding.ASCII.GetBytes("signup_ok");
                            _socket.Send(responseBuffer);
                        }
                        else
                        {
                            byte[] responseBuffer = Encoding.ASCII.GetBytes("username already exists");
                            _socket.Send(responseBuffer);
                            continue;
                        }
                        break;

                    default:
                        // unknown request type
                        byte[] unknownResponse = Encoding.ASCII.GetBytes("unknown request type");
                        _socket.Send(unknownResponse);
                        continue;
                }
            }

            // add the client to the collection of clients
            ClientInfo client = new ClientInfo();
            client.ID = clientId;
            client.Socket = _socket;
            _clients.Add(client);

            Console.WriteLine("Client {0} authenticated. Assigned ID {1}", _socket.RemoteEndPoint.ToString(), clientId);

            // broadcast a message to all clients that a new client has connected
            string connectMessage = $"{clientId} has joined the chat.";

            foreach (var c in _clients)
            {
                if (c.Socket != _socket)
                {
                    byte[] buffer = Encoding.ASCII.GetBytes(connectMessage);
                    c.Socket.Send(buffer);
                }
            }

            while (true)
            {
                try
                {
                    // receive data from the client
                    byte[] buffer = new byte[1024];
                    int bytesRead = _socket.Receive(buffer);
                    if (bytesRead < 0) throw new InvalidOperationException();
                    string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                    Console.WriteLine("Received message from {0}: {1}", _socket.RemoteEndPoint.ToString(), message);

                    // parse the message and handle it according to the protocol
                    string[] parts = message.Split(':');

                    if (parts.Length < 2)
                    {
                        // invalid message format
                        byte[] response = Encoding.ASCII.GetBytes("invalid message format");
                        _socket.Send(response);
                        continue;
                    }
                    else
                    {
                        string sender = parts[0];
                        string recipient = parts[1];
                        string content = parts[2];

                        if (recipient == "bc")
                        {
                            // broadcast the message to all clients
                            string broadcastMessage = $"{sender}:{content}";
                            foreach (var c in _clients)
                            {
                                if (c.Socket != _socket)
                                {
                                    byte[] bufferToSend = Encoding.ASCII.GetBytes(broadcastMessage);
                                    c.Socket.Send(bufferToSend);
                                }
                            }
                        }
                        else
                        {
                            // send the message to the specified client
                            ClientInfo recipientClient = _clients.Find(c => c.ID == recipient);
                            if (recipientClient != null)
                            {
                                string directMessage = $"{sender}:{content}";
                                byte[] bufferToSend = Encoding.ASCII.GetBytes(directMessage);
                                recipientClient.Socket.Send(bufferToSend);
                            }
                            else
                            {
                                // recipient not found
                                byte[] response = Encoding.ASCII.GetBytes("recipient not found");
                                _socket.Send(response);
                                continue;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    // handle the exception
                    Console.WriteLine("Client {0} disconnected.", clientId);
                    _clients.RemoveAll(c => c.ID == clientId);

                    // broadcast a message to all clients that the client has disconnected
                    string disconnectMessage = $"{clientId} has left the chat.";
                    foreach (var c in _clients)
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes(disconnectMessage);
                        c.Socket.Send(buffer);
                    }

                    break;
                }
            }
        }).Start();
    }

    static bool IsValidCredentials(string username, string password, bool isSignUp)
    {
        MongoClient dbClient = new MongoClient("mongodb://localhost:27017");
        IMongoDatabase db = dbClient.GetDatabase("testdb");
        IMongoCollection<BsonDocument> collection = db.GetCollection<BsonDocument>("users");

        BsonDocument document = collection.Find(new BsonDocument("username", username)).FirstOrDefault();

        if (isSignUp)
        {
            if (document == null)
            {
                document = new BsonDocument
                {
                    { "username", username },
                    { "password", password }
                };
                collection.InsertOne(document);
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            if (document != null && document["password"] == password)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}

class ClientInfo
{
    public string ID { get; set; }
    public Socket Socket { get; set; }
}