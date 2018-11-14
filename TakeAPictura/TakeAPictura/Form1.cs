using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TakeAPictura
{
	public partial class Form1 : Form
	{
		// TELLO接続文字列
		private const string CONNECT_TELLO_IP = "192.168.10.1";
		private const string RECIEVE_TELLO_IP = "0.0,0.0";
		private const int CONNECT_TELLO_PORT = 8889;
		private const int RECIEVE_VIDEO_PORT = 11111;

		private UdpClient udpClient = null;

		private bool isStopUpDown = false;
		private bool canUp = true;

		private enum RecieveMode
		{
			OKCancel = 0,
			GetData,
			GetVideo,
			None
		}

		public Form1()
		{
			InitializeComponent();

			var returnCommand = string.Empty;

			// Tello1接続コマンドの送信
			SendCommand("command");

			while(!(returnCommand == "ok" || returnCommand == "error"))
			{
				returnCommand = UdpReciever(CONNECT_TELLO_PORT, CONNECT_TELLO_IP, (int)RecieveMode.OKCancel);
			}
		}

		/// <summary>
		/// コマンドの送信
		/// </summary>
		/// <param name="commandTxt"></param>
		/// <returns></returns>
		private int SendCommand(string commandTxt)
		{
			// UDPクライアントを生成する
			if(udpClient == null)
			{
				udpClient = new UdpClient();
			}

			// TELLOへ送信するコマンドを生成する
			string connectMessage = commandTxt;
			byte[] byteMessage = Encoding.UTF8.GetBytes(connectMessage);

			// TELLOへコマンドを送信する
			int rcv = udpClient.Send(byteMessage, byteMessage.Length, CONNECT_TELLO_IP, CONNECT_TELLO_PORT);

			return rcv;
		}

		/// <summary>
		/// コマンドの受信
		/// </summary>
		/// <returns></returns>
		private string UdpReciever(int recievePort,string recieveIP,int recieveMode)
		{
			var returnCommand = string.Empty;

			// TimeOutを設定する
			udpClient.Client.ReceiveTimeout = 300;
			IPAddress telloAddress = null;

			// IPとポートをバインドする
			telloAddress = IPAddress.Parse(recieveIP);

			// UDPのEndPointを生成する
			var endPoint = new IPEndPoint(telloAddress, recievePort);

			byte[] rcvByte = null;

			try
			{
				// UDPを使用して、TELLOからデータを受信する
				rcvByte = udpClient.Receive(ref endPoint);
			}
			catch (Exception ex)
			{
				return returnCommand;
			}

			if (rcvByte != null && rcvByte.Length > 0)
			{
				// 受信データを文字列に変換する
				returnCommand = Encoding.UTF8.GetString(rcvByte);
			}

			return returnCommand;
		}

		/// <summary>
		/// Take Off
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void button1_Click(object sender, EventArgs e)
		{
			var returnCommand = string.Empty;

			SendCommand("takeoff");

			while (!(returnCommand == "ok" || returnCommand == "error"))
			{
				returnCommand = UdpReciever(CONNECT_TELLO_PORT, CONNECT_TELLO_IP, (int)RecieveMode.OKCancel);
			}
		}

		/// <summary>
		/// Land
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void button2_Click(object sender, EventArgs e)
		{
			SendCommand("land");
			UdpReciever(CONNECT_TELLO_PORT, CONNECT_TELLO_IP, (int)RecieveMode.OKCancel);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void button3_Click(object sender, EventArgs e)
		{
			SendCommand("streamon");
			UdpReciever(CONNECT_TELLO_PORT, CONNECT_TELLO_IP, (int)RecieveMode.OKCancel);
			//UdpReciever(RECIEVE_VIDEO_PORT, RECIEVE_TELLO_IP, (int)RecieveMode.GetVideo);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void button4_Click(object sender, EventArgs e)
		{
			SendCommand("streamoff");
			UdpReciever(CONNECT_TELLO_PORT, CONNECT_TELLO_IP, (int)RecieveMode.OKCancel);
		}

		/// <summary>
		/// バッテリー残量の確認
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void button5_Click(object sender, EventArgs e)
		{
			var isGetBattery = false;
			int battery = -1;

			SendCommand("battery?");

			while(!isGetBattery)
			{
				var returnCommand = UdpReciever(CONNECT_TELLO_PORT, CONNECT_TELLO_IP, (int)RecieveMode.GetData);

				if(int.TryParse(returnCommand,out battery))
				{
					isGetBattery = true;
				}
			}

			this.textBox1.Text = battery.ToString();
		}

		/// <summary>
		/// テローを一定の高さの範囲でUp-Downさせる
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void StartUpDown_Click(object sender, EventArgs e)
		{
			isStopUpDown = false;

			this.StartUpDown.Enabled = false;

			// タスクの実行
			upDownTask = new Task(DoUpDown);
			upDownTask.Start();

		}

		/// <summary>
		/// Up-Downのストップ
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void StopUpDown_Click(object sender, EventArgs e)
		{
			isStopUpDown = true;

			upDownTask.Wait();
			upDownTask.Dispose();

			this.StartUpDown.Enabled = true;
		}

		Task upDownTask;

		/// <summary>
		/// TELLOをUP/DOWNをさせる処理
		/// </summary>
		private void DoUpDown()
		{
			var returnCommand = string.Empty;

			while (!isStopUpDown)
			{
				// 現在の高度を取得する
				this.SendCommand("height?");

				while(true)
				{
					returnCommand = this.UdpReciever(CONNECT_TELLO_PORT, CONNECT_TELLO_IP, (int)RecieveMode.None);

					if(returnCommand.Contains("dm"))
					{
						break;
					}
				}

				var height = CalcTelloHeight(returnCommand);

				if(height == -1)
				{
					break;
				}

				Thread.Sleep(1000);

				if (height >= 130)
				{
					// 高さが130cm以上の場合はdownコマンドで高度を下げる
					this.SendCommand("down 20");
					canUp = false;
				}
				else if(height <= 30)
				{
					// 高さが30cm以下の場合はupコマンドで高度を上げる
					this.SendCommand("up 20");
					canUp = true;
				}
				else
				{
					// 高さが30-130cmの間の場合は、前回と同じコマンドを実行する
					if(canUp)
					{
						var up = 20;

						if(height + 20 > 130)
						{
							up = 130 - height;
						}

						this.SendCommand("up " + up.ToString());
					}
					else
					{
						var down = 20;

						if (height - 20 < 30)
						{
							down = height - 30;
						}

						this.SendCommand("down " + down.ToString());
					}
				}

				returnCommand = this.UdpReciever(CONNECT_TELLO_PORT, CONNECT_TELLO_IP, (int)RecieveMode.None);

				Thread.Sleep(1000);
			}
		}

		/// <summary>
		/// TELLOの現在の高さを計算する
		/// </summary>
		/// <param name="recieveMessage"></param>
		/// <returns></returns>
		private int CalcTelloHeight(string recieveMessage)
		{
			var height = 0;

			// 高度は0dm\r\nの形式で受信されるため、dm\r\nの部分を除去する
			// また、dm = 10cmのため、取得した高度を10倍して、cm単位で処理を行う
			var heightString = recieveMessage.Replace("dm\r\n", "");

			if (!int.TryParse(heightString, out height))
			{
				// 高さが取れなかった場合は異常値を返す
				return -1;
			}

			return height * 10;
		}
	}
}
