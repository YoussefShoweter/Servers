using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;




namespace Server_GUI
{
    public partial class Form1 : Form
    {
        Socket sock;
        Socket acc;
        public Form1()
        {
            InitializeComponent();
            sock =  socket();
        }

        Socket socket()
        {
            return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            sock = socket();
            sock.Bind(new IPEndPoint(IPAddress.Loopback, 6202));
            sock.Listen();

            new Thread(() => {
                acc = sock.Accept();
                MessageBox.Show("Connection Accepted");

                while (true)
                {
                    try
                    {
                        byte[] buffer = new byte[255];
                        int recieved = acc.Receive(buffer, 0, buffer.Length, 0);

                        if (recieved <= 0)
                        {
                            throw new SocketException();

                        }
                        Array.Resize(ref buffer, recieved);

                        Invoke((MethodInvoker)delegate
                        {
                            listBox1.Items.Add(Encoding.Default.GetString(buffer));
                        });
                    }
                    catch {
                        MessageBox.Show("Disconnected");
                        Application.Exit();
                    }
                 
                }
            }).Start(); 

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            byte[] buffer = Encoding.Default.GetBytes(textBox1.Text);
            acc.Send(buffer, 0, buffer.Length, 0);


        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}