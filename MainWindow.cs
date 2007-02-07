/*
 * Created by SharpDevelop.
 * User: Tom Parker
 * Date: 06/02/2007
 * Time: 01:18
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using Glade;
using Gtk;
using System;
using Gecko;
using Pyro;

namespace PyroGui
{
	public class MainWindow
	{
		WebControl webCurr, webDupl;
		[Widget] Frame frmCurrent;
		[Widget] Frame frmDupl;
		[Widget] Label lblStatus;

		public static void render(WebControl web, string content, bool stacktrace)
		{
			web.OpenStream("file:///"+(stacktrace?"#stacktrace":""),"text/html");
			web.AppendData(content);
			web.CloseStream();
		}

		public MainWindow(string[] Args)
		{
			Glade.XML gxml = new Glade.XML(null, "gui.glade", "MainWindow", null);
			gxml.Autoconnect(this);
			
			webCurr = new WebControl();          
			webCurr.Show();
			frmCurrent.Add(webCurr);

			webDupl = new WebControl();          
			webDupl.Show();
			frmDupl.Add(webDupl);

			Bug b = new Bug(0);//int.Parse(Args[0]));
			Bug curr;
			if (Args.Length == 0)
			{
				Bug[] core = b.corebugs();
				curr = core[21];
			}
			else
			{
				curr = new Bug(int.Parse(Args[0]));
			}
			if (curr.triageable())
			{
				Stacktrace st = curr.getStacktrace();
				if (st.usable())
				{
					Bug[] dupe = curr.similar();
					Bug du = null;
					foreach(Bug b2 in dupe)
					{
						if (curr.id == b2.id)
							continue;
						Stacktrace st2 = b2.getStacktrace();
						if (st == st2)
						{
							lblStatus.Text = String.Format("{0} and {1} are duplicates",curr.id,b2.id);
							MainWindow.render(webCurr,curr.raw,true);
							MainWindow.render(webDupl,b2.raw,true);
							du = b2;
							break;
						}
					}
					if (du == null)
					{
						lblStatus.Text = "Can't find match. Need better trace?";
						MainWindow.render(webCurr,curr.raw,true);
					}
					//Console.WriteLine(st);
				}
				else
				{
					lblStatus.Text = "Crap stacktrace";
					MainWindow.render(webCurr,curr.raw,false);
				}
			}
			((Window)gxml.GetWidget("MainWindow")).Maximize();
		}
		
		[STAThread]
		public static void Main(string[] args)
		{
			Application.Init();
			new MainWindow(args);
			Application.Run();
		}

     	public void OnWindowDeleteEvent (object o, DeleteEventArgs args) 
		{
			Application.Quit();
			args.RetVal = true;
		}

		public void OnYesClicked(object o, EventArgs args)
		{
		}
		
		public void OnNoClicked(object o, EventArgs args)
		{
		}
	}
}
