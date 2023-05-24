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
namespace Client_GUI
{
    public partial class Form1 : Form
    {
        Socket sock;
        Socket acc;

        public Form1()
        {
            InitializeComponent();
            sock = socket();
        }
        Socket socket()
        {
            return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MessageBox.Show(textBox1.Text);
            IPEndPoint LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 6202);
            try
            {

                sock.Connect(new IPEndPoint(IPAddress.Parse(textBox1.Text),6202));
                new Thread(()=>
                    {
                        read();
                    }).Start();
                    
            }
            catch
            {
                MessageBox.Show("Connection Failed");

            }


        }
        private void Form1_Load(object sender, EventArgs e)
        {

        }

        void read()
            
        {
            while (true){
                try
                {
                    byte[] buffer = new byte[255];
                    int recieved = sock.Receive(buffer, 0, buffer.Length, 0);

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
                catch
                {
                    MessageBox.Show("Disconnected");
                    Application.Exit();
                }
            }
           

        }

        private void button2_Click(object sender, EventArgs e)
        {
            byte[] data = Encoding.Default.GetBytes(textBox2.Text);
            sock.Send(data,0,data.Length,0);

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}