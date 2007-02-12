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
using System.IO;
using Gecko;
using Pyro;
using System.Collections;
using System.Threading;
using System.Collections.Generic;
using System.Net;

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
			//web.StatusChange += new EventHandler(changeHandler);
			web.NetStart += new EventHandler(NetStateAllHandler);
			frm.Add(web);
		}

		public void changeHandler (object o, EventArgs args)
		{
			Console.WriteLine("changehandler");
			Console.WriteLine(args);
		}

		public void NetStateAllHandler (object o, EventArgs args)
		{
			Console.WriteLine("netstateall");
			Console.WriteLine(args);
		}

		public void render(bool stacktrace, string content)
		{
			web.OpenStream("file:///"+(stacktrace?"#stacktrace":""),"text/html");
			string chunk = "wibble"+content.Substring(0,content.Length>=100?600:content.Length);
			web.AppendData(chunk);
			web.CloseStream();
			web.Show();
			while (Gtk.Application.EventsPending ())
				Gtk.Application.RunIteration ();
			Console.WriteLine(chunk);
		}
	
		public void render(bool stacktrace)
		{
			render(stacktrace,bug.raw);
		}
	}
	
	public class MainWindow
	{
		BugDisplay curr,dupl;
		[Widget] Frame frmCurrent;
		[Widget] Frame frmDupl;
		[Widget] Label lblStatus;

		Queue<Bug> todo;
		Bugzilla bugz;

		static ThreadNotify notify;

		public enum Response
		{
			LoginSuccess,
			LoginFailure,
			BadStacktrace,
			Duplicate
		}

		public struct Event
		{
			public Response r;
			public Bug b, dup;
			public string text;

			public Event(Response r, string info) : this(r,null,null,info) {}

			public Event(Response r, Bug one, Bug two, string info)
			{
				this.r = r;
				b = one;
				dup = two;
				text = info;
			}
		}

		public Queue <Event> events;
	
		public MainWindow(string[] Args)
		{
			Glade.XML gxml = new Glade.XML(null, "gui.glade", "MainWindow", null);
			gxml.Autoconnect(this);

			events = new Queue<Event>();
			
			curr = new BugDisplay(frmCurrent);
			curr.render(false,"hello world");
			dupl = new BugDisplay(frmDupl);

			((Window)gxml.GetWidget("MainWindow")).Maximize();
			((Window)gxml.GetWidget("MainWindow")).ShowAll();
			//GlobalProxySelection.Select = new WebProxy("http://taz:8118");
			
			bugz = new Bugzilla("http://bugzilla.gnome.org/");

			todo = new Queue<Bug>();
			if (Args.Length!=0)
				todo.Enqueue(new Bug(int.Parse(Args[0]),bugz));
			Thread thr = new Thread (new ThreadStart (processTask));
		    thr.Start ();
			//GLib.Idle.Add(new GLib.IdleHandler(processTask));
			notify = new ThreadNotify (new ReadyEvent (ready));
		}

		void ready ()
		{
			if (events.Count>0)
			{
				Event e = events.Dequeue();
				lblStatus.Text = e.text;
				this.dupl = null;
				switch(e.r)
				{
					case Response.LoginFailure:
					case Response.LoginSuccess:
						break;
					case Response.Duplicate:
						this.dupl.bug = e.dup;
						this.dupl.render(true);
						Console.WriteLine("dupl: {0}",e.dup.id);
						goto case Response.BadStacktrace;
					case Response.BadStacktrace:
						this.curr.bug = e.b;
						this.curr.render(this.dupl!=null);
						Console.WriteLine("curr: {0}",e.b.id);
						break;
				}
			}
		}

		private void postEvent(Event e)
		{
			events.Enqueue(e);
			notify.WakeupMain();
		}

		public void processTask()
		{
			if (!bugz.loggedIn)
			{
				try
				{
					if (!bugz.login("palfrey@tevp.net","epsilon"))
					{
						postEvent(new Event(Response.LoginFailure,"Login failure"));
						return;
					}
				}
				catch (WebException e)
				{
					postEvent(new Event(Response.LoginFailure,((HttpWebResponse)e.Response).StatusDescription));
					return;
				}
			}
			Bug bug;
			if (todo.Count == 0)
			{
				Bug[] core = new Bug(0,bugz).corebugs();
				bug = core[21];
			}
			else
			{
				bug = todo.Dequeue();
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
							postEvent(new Event(Response.Duplicate,bug,b2,String.Format("{0} and {1} are duplicates?",bug.id,b2.id)));
							break;
						}
					}
					if (du == null)
					{
						if (bug["Status"]!="NEEDINFO")
							postEvent(new Event(Response.BadStacktrace,bug,null,"Can't find match. Need better trace?"));
						else
							Console.WriteLine("Already needinfo, need better trace");
					}
					//Console.WriteLine(st);
				}
				else
					postEvent(new Event(Response.BadStacktrace,bug,null, "Crap stacktrace?"));
			}
			return;
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
