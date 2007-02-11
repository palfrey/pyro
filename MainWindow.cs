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
using System.Collections;
using System.Threading;

namespace PyroGui
{
	class BugDisplay
	{
		public WebControl web;
		public Bug bug;

		public BugDisplay(Frame frm)
		{
			web = new WebControl();          
			web.Show();
			frm.Add(web);
		}

		public void render(bool stacktrace)
		{
			Gtk.Application.Invoke (delegate {
				web.OpenStream("file:///"+(stacktrace?"#stacktrace":""),"text/html");
				web.AppendData(bug.raw);
				web.CloseStream();
			});
		}
	}
	
	public class MainWindow
	{
		BugDisplay curr,dupl;
		[Widget] Frame frmCurrent;
		[Widget] Frame frmDupl;
		[Widget] Label lblStatus;

		Queue todo;
		Bugzilla bugz;

	
		public MainWindow(string[] Args)
		{
			Glade.XML gxml = new Glade.XML(null, "gui.glade", "MainWindow", null);
			gxml.Autoconnect(this);
			
			curr = new BugDisplay(frmCurrent);
			dupl = new BugDisplay(frmDupl);

			((Window)gxml.GetWidget("MainWindow")).Maximize();
			
			bugz = new Bugzilla("http://bugzilla.gnome.org/");
			bugz.login("palfrey@tevp.net","epsilon");

			todo = new Queue();
			if (Args.Length!=0)
				todo.Enqueue(new Bug(int.Parse(Args[0]),bugz));
			GLib.Idle.Add(new GLib.IdleHandler(processTask));
			//processTask(null);
			return;
		}

		public bool processTask()
		{
			Bug bug;
			if (todo.Count == 0)
			{
				Bug[] core = new Bug(0,bugz).corebugs();
				bug = core[21];
			}
			else
			{
				bug = (Bug)todo.Dequeue();
			}
			if (bug.triageable())
			{
				Stacktrace st = bug.getStacktrace();
				curr.bug = bug;
				if (st.usable())
				{
					Bug[] dupe = bug.similar();
					Bug du = null;
					foreach(Bug b2 in dupe)
					{
						if (bug.id == b2.id)
							continue;
						Stacktrace st2 = b2.getStacktrace();
						if (st == st2)
						{
							lblStatus.Text = String.Format("{0} and {1} are duplicates?",bug.id,b2.id);
							curr.render(true);
							dupl.bug = b2;
							dupl.render(true);
							du = b2;
							break;
						}
					}
					if (du == null)
					{
						if (bug["Status"]!="NEEDINFO")
						{
							lblStatus.Text = "Can't find match. Need better trace?";
							curr.render(true);
						}
						else
							Console.WriteLine("Already needinfo, need better trace");
					}
					//Console.WriteLine(st);
				}
				else
				{
					lblStatus.Text = "Crap stacktrace?";
					curr.render(false);
				}
			}
			return false;
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
			curr.bug.setBadStacktrace();	
			
		}
		
		public void OnNoClicked(object o, EventArgs args)
		{
		}

		protected void OnOpenUri (object o, OpenUriArgs args)
		{
			args.RetVal = true; /* don't load */
		}
	}
}
