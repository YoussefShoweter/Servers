using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Server
{
    class Program
    {
        static int Port = 5423;
        static IPAddress ip;
        static string Name = "Server";
        static List<Socket> sockets = new List<Socket>();
        static List<string> ConnectedUsers = new List<string>();
        static List<string> ActiveUsers = new List<string>();
        static List<string> OldPosition = new List<string>();
        static int minplayers = 2;
        static int Numofusers;
        const string CONNECTION_STRING_1 = "mongodb+srv://19p3041:admin123@cluster0.lzbu4ip.mongodb.net/";
        const string CONNECTION_STRING_2 = "mongodb+srv://19p3041:admin123@cluster0.ttvx1yp.mongodb.net/";
        static MongoClient DBConnection = new MongoClient(CONNECTION_STRING_1);
        static MongoClient DBConnectionPrimary = new MongoClient(CONNECTION_STRING_1);
        static MongoClient DBConnectionSecondary = new MongoClient(CONNECTION_STRING_2);
        static int instanceNo;
        static bool GameOver = false;

        //Main
        static void Main(string[] args)
        {
            instanceNo = GetGame();
            Console.WriteLine(instanceNo);
            Thread updategame = new Thread(() => IncrementGameCount(DBConnectionPrimary));
            updategame.Start();
            Thread updategame2 = new Thread(() => IncrementGameCount(DBConnectionSecondary));
            updategame2.Start();

            bool startGame = false;
            Console.WriteLine("Your Ip is : " + GetIP());
            Console.WriteLine("The server listens on Port :" + Port);
            int inputPort = Port;
          
            ip = IPAddress.Parse(GetIP());
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sock.Bind(new IPEndPoint(ip, Port));
            Console.WriteLine("Waiting for Connections");



            try
            {
                while (true)
                {
                    bool reconnect = false;

                   

                    // Set the timeout to 10 seconds
                    int timeout = 5000;

                    // Server starts listening
                    sock.Listen(0);
                    Socket Sck;
                    // Wait for incoming connections with a timeout
                    while (true)
                    {
                        if (sock.Poll(timeout, SelectMode.SelectRead))
                        {
                            // Accept the incoming connection
                            Sck = sock.Accept();


                            // Process the incoming connection
                            // ...
                            break; // break out of the inner loop once a connection has been accepted
                        }
                        else
                        {
                            Sck = null;
                            // Timeout occurred, handle it accordingly
                            break; // break out of the inner loop and wait for another incoming connection
                        }
                    }


                    if (Sck != null)
                    { //Adding user to connected users
                        Byte[] buffer = new Byte[255];
                        int recievedName = Sck.Receive(buffer, 0, buffer.Length, 0);
                        Array.Resize(ref buffer, recievedName);
                        string Name = Encoding.Default.GetString(buffer);

                        //Checking for re connections
                        if (ConnectedUsers.Contains(Name))
                        {

                            int index = ConnectedUsers.IndexOf(Name);
                            ActiveUsers.Insert(index, Name);
                            sockets.Insert(index, Sck);
                            reconnect = true;

                            Console.WriteLine("The connected users to the server are:\n");
                            for (int i = 0; i < ActiveUsers.Count; i++)
                            {
                                Console.WriteLine(ActiveUsers[i] + '\n');
                            }
                            Numofusers = sockets.Count();
                            GetGameCount(Sck);
                            sendmyoldPos(index, Name);


                            //Get Name from Database



                        }
                        else
                        {

                            sockets.Add(Sck);
                            ConnectedUsers.Add(Name);
                            ActiveUsers.Add(Name);
                            OldPosition.Add("");
                            Console.WriteLine("Connected to : " + Name + "\n");
                            Console.WriteLine("The connected users to the server are:\n");
                            for (int i = 0; i < ConnectedUsers.Count; i++)
                            {
                                Console.WriteLine(ConnectedUsers[i] + '\n');

                            }
                            Numofusers = sockets.Count();

                            GetGameCount(Sck);
                            sendstartPos(Numofusers, Sck);

                        }

                        //checkifCanPlay(startGame,reconnect);



                        Thread rec = new Thread(() => recv(Sck, Name));
                        rec.Start();
                        Thread sen = new Thread(() => send(startGame, reconnect));
                        sen.Start();



                        if (Numofusers >= minplayers)
                        {

                            startGame = true;

                        }
                    }
                    else if (GameOver == false) { continue; }
                    else
                    {
                        Console.WriteLine("Quit from main");
                        throw new IOException();
                    }
                }
            }
            catch (IOException e)
            {
                Console.WriteLine("Game Over");

            }
            catch (Exception ex)
            {
                Console.WriteLine("an exeption occured");
            }


        }

        //Helper Functions
        static string GetIP()
        {
            string strHostName = System.Net.Dns.GetHostName();
            IPHostEntry ipentry = System.Net.Dns.GetHostEntry(strHostName);
            IPAddress[] iPAddresses = ipentry.AddressList;
            return iPAddresses[iPAddresses.Length - 1].ToString();
        }
        static void send(bool startGame, bool recconect)
        {
            Thread.Sleep(1000);

            for (int i = 0; i < sockets.Count; i++)
            {
                if (startGame)
                {
                    Byte[] confirmation = Encoding.Default.GetBytes("Now you can play");
                    sockets[i].Send(confirmation, 0, confirmation.Length, 0);
                }
            }

            while (true)
            {
                byte[] data = Encoding.Default.GetBytes(" " + Name + " : " + Console.ReadLine());

                for (int i = 0; i < sockets.Count; i++)
                {
                    sockets[i].Send(data, 0, data.Length, 0);
                }
            }

        }
        static void recv(Socket sock, string name)
        {
            Thread.Sleep(1000);
            bool Connected = true;
            int index = 0;
            string lastMEssageBeforeDisc = "";

            for (int i = 0; i < sockets.Count; i++)
            {

                if (sockets[i] == sock)
                {
                    index = i;
                }
            }
            while (Connected)
            {
                Byte[] buffer = new Byte[1024];

                try
                {

                    int rec = sock.Receive(buffer, 0, buffer.Length, 0);
                    if (rec <= 0)
                    {
                        throw new Exception();
                    }
                    Array.Resize(ref buffer, rec);

                    for (int i = 0; i < sockets.Count; i++)
                    {
                        if (sockets[i] != sock)
                        {
                            sockets[i].Send(buffer, 0, buffer.Length, 0);
                        }
                    }
                    OldPosition[index] = Encoding.Default.GetString(buffer);

                    lastMEssageBeforeDisc = Encoding.Default.GetString(buffer);
    
                    //Console.WriteLine("\n " + Encoding.Default.GetString(buffer));
                }

                // A disconnection occured
                catch(Exception e) 
                {
                    Console.WriteLine(e.ToString());
                    try
                    {
                        if (sockets.Count() == 1)
                        {
                            Console.WriteLine("EveryOne Left ");
                            sockets.RemoveAt(0);
                            ActiveUsers.RemoveAt(0);
                            reset();
                            Connected = false;
                            continue;



                        }

                        else
                        {
                            sockets.RemoveAt(index);
                            ActiveUsers.RemoveAt(index);
                            Numofusers = sockets.Count;
                        }


                    }
                    catch (Exception ex)
                    {
                    }

                    for (int i = 0; i < sockets.Count; i++)
                    {

                        Byte[] stop = Encoding.Default.GetBytes("STOP");
                        sockets[i].Send(stop, 0, stop.Length, 0);

                    }

                    Console.WriteLine((name + " has disconnected\n" + "this is the old position for " + name + OldPosition[index]));
                    // Viewing the current Connected clients
                    Console.WriteLine("The connected users to the server are:\n");


                    for (int i = 0; i < ActiveUsers.Count; i++)
                    {
                        Console.WriteLine(ActiveUsers[i] + '\n');

                    }
                    try
                    {       //adding to Db
                        if (lastMEssageBeforeDisc != "")
                        {
                            string[] parts1 = lastMEssageBeforeDisc.Split(',');
                            float x1 = float.Parse(parts1[0]);
                            float y1 = float.Parse(parts1[1]);
                            float z1 = float.Parse(parts1[2]);
                            Console.WriteLine(x1 + "" + y1 + "" + z1 + "" + name);
                            Thread savecoor = new Thread(() => SaveCoordinates(x1, y1, z1, name, DBConnection));
                            savecoor.Start();
                            Thread savecoor2 = new Thread(() => SaveCoordinates(x1, y1, z1, name, DBConnectionSecondary));
                            savecoor2.Start();

                            ;
                        }


                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Db input error");
                    }



                    Connected = false;

                }
            }
        }
        static void sendstartPos(int playernum, Socket socket)
        {
            if (playernum == 1)
            {
                Byte[] coor = Encoding.Default.GetBytes("-2,1,0");
                socket.Send(coor, 0, coor.Length, 0);



            }
            else
            {
                Byte[] coor = Encoding.Default.GetBytes("2,1,0");
                socket.Send(coor, 0, coor.Length, 0);


            }
        }

        //send my old position to me when i reconnect
        static void sendmyoldPos(int index, string playername)
        {
            (float x, float y, float z) coordintes = GetCoordinates(playername);
            Console.WriteLine("\n********************\n" + coordintes + "\n" + "^^^^^^^^^^^^^^^^^^^^^^");
            Byte[] confirmation = Encoding.Default.GetBytes(coordintes.x + "," + coordintes.y + "," + coordintes.z);



            //Byte[] confirmation = Encoding.Default.GetBytes(OldPosition[index]);
            sockets[index].Send(confirmation, 0, confirmation.Length, 0);

        }

        public static void GetGameCount(Socket Sck)
        {
            Byte[] Number = BitConverter.GetBytes(instanceNo);
            Sck.Send(Number, 0, Number.Length, 0);
        }

        public static void IncrementGameCount(MongoClient DBConnection)
        {
            try
            {
                // create a MongoDB client and database
                IMongoDatabase database = DBConnection.GetDatabase("GameDB");
                IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>("Game#");

                var filter = Builders<BsonDocument>.Filter.Empty;
                var document = collection.Find(filter).FirstOrDefault();

                if (document == null)
                {
                    // insert new document with game count of 1
                    var newDocument = new BsonDocument("Game#", 1);
                    collection.InsertOne(newDocument);
                }
                else
                {
                    // increment game count and update document
                    int currentCount = document["Game#"].AsInt32;
                    var update = Builders<BsonDocument>.Update.Set("Game#", currentCount + 1);
                    collection.UpdateOne(filter, update);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Faliure of Current DB");

            }
            
        }

        public static int GetGame()
        {
            IMongoDatabase database;
            // create a MongoDB client and database
            try
            {
                database = DBConnection.GetDatabase("GameDB");
            }
            catch (Exception ex)
            {
                Thread recover = new Thread(() =>  Recover(DBConnection));
                recover.Start();

                if (DBConnection == DBConnectionPrimary) {
                    DBConnection = DBConnectionSecondary;
                database = DBConnection.GetDatabase("GameDB");
                Console.WriteLine("The Second Database is primary ");

            }
                else
                {

                    DBConnection = DBConnectionPrimary;
                    database = DBConnection.GetDatabase("GameDB");
                    Console.WriteLine("The first Database is primary ");
                }


            }
            IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>("Game#");
            var filter = Builders<BsonDocument>.Filter.Empty;
            var documents = collection.Find(filter).FirstOrDefault();

            if (documents == null)
            {
                return 0;
            }
            else
            {
                int x = documents["Game#"].AsInt32;
                return x;
            }
        }
     
        public static void SaveCoordinates(float x, float y, float z, string UserName, MongoClient DB)
        {

            // Create a new document
            var document = new BsonDocument
            {
            { "x", new BsonDouble(x) },
            { "y", new BsonDouble(y) },
            { "z",new BsonDouble(z) },
            { "UserName", UserName }
        };
            IMongoDatabase database;

            // Get the collection
            try
            {
                 database = DB.GetDatabase("GameDB");
                IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>("coordinates");

                // Define the filter for the document to replace
                var filter = Builders<BsonDocument>.Filter.Eq("UserName", UserName);

                var existingDocument = collection.Find(filter).FirstOrDefault();
                if (existingDocument == null)
                {

                    collection.InsertOne(document);
                }
                else
                {

                    // Replace the document if it already exists, or insert a new one if it doesn't

                    collection.ReplaceOne(filter, document, new UpdateOptions { IsUpsert = true });

                }
            }
            catch(Exception ex) 
            {

                Console.WriteLine("Faliure of Current DB");

            }



        }

        public static (float x, float y, float z) GetCoordinates(string username)
        {
            // Get the collection
            IMongoDatabase database;
            // create a MongoDB client and database
            try
            {
                database = DBConnection.GetDatabase("GameDB");
            }
            catch (Exception ex)
            {
                Thread recover = new Thread(() => Recover(DBConnection));
                recover.Start();

                if (DBConnection == DBConnectionPrimary)
                {
                    DBConnection = DBConnectionSecondary;
                    database = DBConnection.GetDatabase("GameDB");
                    Console.WriteLine("The Second Database is primary ");
                }
                else
                {
                    DBConnection = DBConnectionPrimary;
                    database = DBConnection.GetDatabase("GameDB");
                    Console.WriteLine("The first Database is primary ");
                }


            }
            IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>("coordinates");

            // Define the filter for the document to retrieve
            var filter = Builders<BsonDocument>.Filter.Eq("UserName", username);
            // Retrieve the document that matches the filter
            try
            {

                var existingDocument = collection.Find(filter).FirstOrDefault();
                // If no document matches the filter, return null
                if (existingDocument == null)
                {
                    return (float.NaN, float.NaN, float.NaN);
                }

                // Get the values of the x, y, and z fields from the document as floats
                float x = Convert.ToSingle(existingDocument["x"].AsDouble);
                float y = Convert.ToSingle(existingDocument["y"].AsDouble);
                float z = Convert.ToSingle(existingDocument["z"].AsDouble);

                // Return the coordinates as a tuple
                return (x, y, z);
            }

            catch (Exception ex)
            {
                Console.WriteLine("Errrrrrrrrrror   " + ex.Message);
                return (float.NaN, float.NaN, float.NaN);

            }

        }


        public static void Recover(MongoClient DBC)
        {
            bool recovered=false;
            while (!recovered)
            {
                try {

                    IMongoDatabase database = DBC.GetDatabase("GameDB");

                    string sourceConnectionString ;
                    string targetConnectionString;
                    if (DBC == DBConnectionPrimary)
                    {
                         sourceConnectionString = CONNECTION_STRING_2;
                         targetConnectionString = CONNECTION_STRING_1;
                    }
                    else
                    {
                         sourceConnectionString = CONNECTION_STRING_1;
                         targetConnectionString = CONNECTION_STRING_2;
                    }



                    
                    string databaseName = "GameDB";

                    // export data from the source cluster
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = "mongodump";
                    startInfo.Arguments = $"--uri {sourceConnectionString} --db {databaseName}";
                    startInfo.RedirectStandardOutput = true;
                    Process process = new Process();
                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();

                    // import data into the target cluster
                    startInfo = new ProcessStartInfo();
                    startInfo.FileName = "mongorestore";
                    startInfo.Arguments = $"--uri {targetConnectionString} --drop";
                    startInfo.RedirectStandardInput = true;
                    process = new Process();
                    process.StartInfo = startInfo;
                    process.Start();
                    string dumpOutput = process.StandardOutput.ReadToEnd();
                    process.StandardInput.WriteLine(dumpOutput);
                    process.StandardInput.Close();
                    process.WaitForExit();

                    recovered = true;
                
                }
                catch (Exception ex) {
                    Thread.Sleep(5000);
                }
            }
        }


        public static void reset()
        {
            sockets = new List<Socket>();
            ConnectedUsers = new List<string>();
            ActiveUsers = new List<string>();
            OldPosition = new List<string>();
            Numofusers = 0;
            IncrementGameCount(DBConnection);
        } 
    }
}
