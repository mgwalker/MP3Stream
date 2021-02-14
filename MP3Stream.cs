using System;
using System.Net.Sockets;
using System.Text;

namespace MP3Stream
{
	/**/
	public class MP3Server
	{
		const int PORT = 9999;

		public static void Main()
		{
			TcpListener server = new TcpListener(PORT);
			server.Start();

			while(true)
			{
				MP3Streamer streamer = new MP3Streamer(server.AcceptTcpClient().GetStream());//.AcceptSocket());
				//Console.WriteLine(streamer.GetWholeList(@"h:\Broadcast") + " files in the queue");
				Console.WriteLine(streamer.GetWholeList(@"d:\music\Broadcast") + " files in the queue");

				streamer.PlayWholeList("JF Radidio","Music","128");
			}
		}
	}//*/

	public class MP3Streamer
	{
		private NetworkStream client;
		private int listCounter;
		private string[] wholeList;
		private string[] playList;
		private static int metaint = 16000;
		private bool keepPlaying;

		public MP3Streamer(NetworkStream _CLIENT)
		{
			client = _CLIENT;
			wholeList = null;
			playList = null;
			listCounter = 0;
			keepPlaying = true;
		}

		public byte[] ParseID3(string FILENAME, ref int len)
		{
			byte[] ret = new byte[128];
			string str;
			string author = "", title = "";

			System.IO.FileStream file = new System.IO.FileStream(FILENAME, System.IO.FileMode.Open);
			file.Seek(-128, System.IO.SeekOrigin.End);

			file.Read(ret, 0, 128);
			str = Encoding.ASCII.GetString(ret);
			if(str.Substring(0,3).ToUpper() == "TAG")
			{
				title = str.Substring(3, 30).Trim('\0').Trim() + "';";
				author = "StreamTitle='" + str.Substring(33, 30).Trim('\0').Trim();
			}
			else
			{
				file.Seek(0, System.IO.SeekOrigin.Begin);
				file.Read(ret, 0, 3);
				str = Encoding.ASCII.GetString(ret,0,3);

				//reserved for ID3v2
				byte[] buff = new byte[128];
				
				if(str.ToUpper() == "ID3")
				{
					file.Read(buff, 0, 2);
					file.Read(buff, 0, 1);
					
					if(System.Convert.ToInt16(buff[0]) == 0)
					{
						Console.WriteLine("ID3v2 Type I");
						byte[] sze = new byte[4];
						file.Read(sze, 0, 4);
						int size = Convert.ToInt16(Encoding.ASCII.GetString(sze));
						Console.WriteLine(size);

						title = "ID3v2";
						author = "Type I";
					}
				}
			}

			if(title != "" && author != "")
			{
				int length = title.Length + author.Length + 3;
				int fByte = (int)System.Math.Ceiling(length/16) + 1;
				int padding = (fByte * 16) - length;
				if(padding == 0)
				{
					fByte++;
					padding = 16;
				}
				length = length + 1 + padding;
				len = length;

				ret = new byte[length];
				for(int i = 0; i < ret.Length; i++)
					ret[i] = 0;
				ret[0] = (byte)fByte;
				byte[] tmp = Encoding.ASCII.GetBytes(author + " - " + title);
				tmp.CopyTo(ret, 1);
			}
			else
			{
				string[] x = FILENAME.Split('\\');
				author = "StreamTitle='" + x[x.Length - 1] + "';";

				int length = author.Length + 1;
				int fByte = (int)System.Math.Ceiling(length/16) + 1;
				int padding = (fByte * 16) - length;
				if(padding == 0)
				{
					fByte++;
					padding = 16;
				}
				length = length + 1 + padding;
				len = length;

				ret = new byte[length];
				for(int i = 0; i < ret.Length; i++)
					ret[i] = 0;
				ret[0] = (byte)fByte;
				byte[] tmp = Encoding.ASCII.GetBytes(author);
				tmp.CopyTo(ret, 1);
			}

			file.Close();
			return ret;
		}

		public void PlayWholeList(string name, string genre, string br)
		{
			try
			{
				string header = "ICY 200 OK\r\n";
				header += "icy-notice1:<BR>Hello there!<BR>\r\n";
				header += "icy-name:" + name + "\r\n";
				header += "icy-genre:" + genre + "\r\n";
				header += "icy-metaint:" + metaint + "\r\n";
				header += "icy-pub:1\r\nicy-br:" + br + "\r\n\r\n";
				client.Write(Encoding.ASCII.GetBytes(header),0,header.Length);

				int lastIndex = -1;
				//int index = -1;
				while(keepPlaying)
				{
					int index = new System.Random().Next(0,wholeList.Length - 1);
					while(index == lastIndex)
						index = new System.Random().Next(0,wholeList.Length - 1);
					lastIndex = index;/** /
					index++;
					if(index >= wholeList.Length)
						index = 0;*/
					int len = 0;
					byte[] mdata = ParseID3(wholeList[index], ref len);
					Console.Write(index + ") ");
					Console.WriteLine(Encoding.ASCII.GetChars(mdata));

					System.IO.FileStream fs = new System.IO.FileStream(wholeList[index],System.IO.FileMode.Open);

					byte[] buffer = new byte[metaint];
					int read = fs.Read(buffer,0,metaint);

					int x = 0;
					while(read > 0 && x < 3)
					{
						x++;
						//Console.WriteLine("Read in " + read + " bytes");
						client.Write(buffer, 0, metaint);

						for(int i = 0; i < buffer.Length; i++)
							buffer[i] = 0;

						client.Write(mdata, 0, len);
						read = fs.Read(buffer,0,metaint);
						System.Threading.Thread.Sleep(System.TimeSpan.FromMilliseconds(900));
					}

					fs.Close();
				}
			}
			catch(Exception e)
			{
				//Console.WriteLine("There was an error: " + e.ToString());
			}
		}

		public int GetWholeList(string baseDir)
		{
			System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo(baseDir);
			return GetDirFiles(dir, baseDir.Substring(0, baseDir.LastIndexOf(@"\")));
		}

		public int GetDirFiles(System.IO.DirectoryInfo dir, string lead)
		{
			System.IO.FileInfo[] files = dir.GetFiles();
			int ret = 0;

			for(int i = 0; i < files.Length; i++)
			{
				if(files[i].Name.Length > 3)
				{
					if(files[i].Name.Substring(files[i].Name.Length-3,3).ToUpper() == "MP3")
					{
						string[] tmp = wholeList;
						if(tmp != null)
						{
							wholeList = new String[wholeList.Length + 1];
							tmp.CopyTo(wholeList, 0);
						}
						else
							wholeList = new String[1];

						wholeList[wholeList.Length - 1] = lead + "\\" + dir.Name + "\\" + files[i].Name;
						ret++;
					}
				}
			}

			System.IO.DirectoryInfo[] dirs = dir.GetDirectories();
			for(int i = 0; i < dirs.Length; i++)
				if(dirs[i].Name.ToLower() != "share")//disallow directory
					ret += GetDirFiles(dirs[i], lead + "\\" + dir.Name);

			return ret;
		}
	}
}
