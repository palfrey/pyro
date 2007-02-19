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
		//public HTML web;
		public Bug bug;

		public BugDisplay(Frame frm)
		{
			web = new WebControl();
			//web = new HTML();
			web.Show();
			//web.StatusChange += new EventHandler(changeHandler);
			//web.NetStart += new EventHandler(NetStateAllHandler);
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

		public void showBug(bool stacktrace)
		{
			web.LoadUrl("file://"+bug.localpath()+(stacktrace?"#stacktrace":"#c0"));
		}
		
		public void loadURL(string url)
		{
			web.LoadUrl(url);
		}
		
		/*public void render(bool stacktrace, string content)
		{
			string chunk = content;//"wibble"+content.Substring(0,content.Length>=100?600:content.Length);
			throw new Exception();
			//Console.WriteLine(chunk);
			//web.RenderData(chunk,"http://bugzilla.gnome.org/show_bug.cgi?id="+this.bug.id+(stacktrace?"#stacktrace":""),"text/html");
			web.OpenStream("file:///"+(stacktrace?"#stacktrace":""),"text/html");
			Console.WriteLine("did openstream");
			web.AppendData(chunk);
			Console.WriteLine("did append");
			web.CloseStream();
			Console.WriteLine("did closestream");
			//web.Show();
			//web.LoadFromString(content);
			while (Gtk.Application.EventsPending ())
				Gtk.Application.RunIteration ();
		}*/
	
		public void clear()
		{
			bug = null;
			loadURL("about:blank");
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

		bool doing = false;

		static ThreadNotify notify;

		public enum BugEvent
		{
			LoginSuccess,
			LoginFailure,
			NoMatch,
			BadStacktrace,
			Duplicate,
		}

		public enum BugChange
		{
			MarkBad,
			MarkDupe
		}

		public struct Event
		{
			public BugEvent r;
			public Bug b, dup;
			public string text;

			public Event(BugEvent r, string info) : this(r,null,null,info) {}

			public Event(BugEvent r, Bug one, Bug two, string info)
			{
				this.r = r;
				b = one;
				dup = two;
				text = info;
			}
		}

		public Queue <Event> events;

		public struct Delta
		{
			public BugChange c;
			public Bug b, dup;

			public Delta(BugChange r) : this(r,null,null) {}

			public Delta(BugChange r, Bug one, Bug two)
			{
				c = r;
				b = one;
				dup = two;
			}
		}
		public Queue <Delta> deltas;
	
		public MainWindow(string[] Args)
		{
			Glade.XML gxml = new Glade.XML(null, "gui.glade", "MainWindow", null);
			gxml.Autoconnect(this);

			events = new Queue<Event>();
			deltas = new Queue<Delta>();
			
			curr = new BugDisplay(frmCurrent);
			//curr.render(false,"hello world");
			dupl = new BugDisplay(frmDupl);

			((Window)gxml.GetWidget("MainWindow")).Maximize();
			((Window)gxml.GetWidget("MainWindow")).ShowAll();
			//GlobalProxySelection.Select = new WebProxy("http://taz:8118");
			
			bugz = new Bugzilla("http://bugzilla.gnome.org/");
			BugDB.bugz = bugz;

			todo = new Queue<Bug>();
			if (Args.Length!=0)
			{
				int id = int.Parse(Args[0]);
				Bug b = Bug.getExisting(id);
				if (b == null)
					b = new Bug(id,bugz);
				todo.Enqueue(b);
			}
			ready();
			GLib.Idle.Add(new GLib.IdleHandler(processTask));
			notify = new ThreadNotify (new ReadyEvent (ready));
		}

		void ready ()
		{
			if (events.Count>0)
			{
				Console.WriteLine("grab event");
				doing = true;
				Event e = events.Dequeue();
				lblStatus.Text = e.text;
				this.dupl.bug = null;
				this.dupl.clear();
				switch(e.r)
				{
					case BugEvent.LoginFailure:
					case BugEvent.LoginSuccess:
						break;
					case BugEvent.Duplicate:
						Console.WriteLine("dupl: {0}",e.dup.id);
						this.dupl.bug = e.dup;
						this.dupl.showBug(true);
						//this.dupl.loadURL("http://bugzilla.gnome.org/show_bug.cgi?id="+this.dupl.bug.id+"#stacktrace");
						goto case BugEvent.BadStacktrace;
					case BugEvent.NoMatch:
					case BugEvent.BadStacktrace:
						Console.WriteLine("curr: {0}",e.b.id);
						this.curr.bug = e.b;
						//this.curr.loadURL("http://bugzilla.gnome.org/show_bug.cgi?id="+this.curr.bug.id+(this.dupl.bug==null?"":"#stacktrace"));
						this.curr.showBug(e.r==BugEvent.NoMatch || this.dupl.bug!=null);
						break;
				}
				Console.WriteLine("grab event done");
			}
			else
			{
				lblStatus.Text = "No more events";
				this.curr.clear();
				this.dupl.clear();
			}
		}

		private void postEvent(Event e)
		{
			Console.WriteLine("\npostEvent\n");
			events.Enqueue(e);
			if (!doing)
			{
				Console.WriteLine("\nnotify\n");
				notify.WakeupMain();
			}
		}

		private void postChange(Delta d)
		{
			Console.WriteLine(d);
			deltas.Enqueue(d);
		}

		bool taskLock = false;
		
		public bool processTask()
		{
			if (taskLock)
				return true;
			if (!bugz.loggedIn)
			{
				try
				{
					if (!bugz.login("palfrey@tevp.net","epsilon"))
					{
						postEvent(new Event(BugEvent.LoginFailure,"Login failure"));
						return false;
					}
				}
				catch (WebException e)
				{
					postEvent(new Event(BugEvent.LoginFailure,((HttpWebResponse)e.Response).StatusDescription));
					return false;
				}
			}
			if (deltas.Count>0)
			{
				Console.WriteLine("delta happened");
				taskLock = true;
				Delta d = deltas.Dequeue();
				switch(d.c)
				{
					case BugChange.MarkBad:
						d.b.setBadStacktrace(new Response(endTask));
						break;
					case BugChange.MarkDupe:
						d.b.setDupe(new Response(endTask),d.dup);
						break;
					default:
						throw new Exception();
				}
				return true;
			}
			if (events.Count>=2)
				return true;
			if (events.Count<2)
			{
				Console.WriteLine("\nlooking for bugs\n");
				taskLock = true;
				if (todo.Count == 0)
					new Bug(0,bugz).corebugs(new Response(extraBugs));
				else
					nextBug();
			}
			return true;
		}

		private void endTask(object res, object data, Response r)
		{
			taskLock = false;
		}

		private void extraBugs(object res, object data, Response r)
		{
			Bug []bugs = (Bug[])res;
			foreach(Bug b in bugs)
			{
				todo.Enqueue(b);
			}
			nextBug();
		}

		private Bug bug = null;
		private Stacktrace st = null;

		private void nextBug()
		{
			bug = todo.Dequeue();
			bug.triageable(new Response(nextTriageable));
		}

		private void nextTriageable(object res, object data, Response r)
		{
			if (((bool)res)==true)
			{
				Console.WriteLine("{0} is triageable",bug.id);
				bug.getStacktrace(new Response(grabStacktrace,r,bug));
			}
			else
			{
				Console.WriteLine("{0} is not triageable",bug.id);
				bug.describe();
				taskLock = false;
			}
		}

		private void grabStacktrace(object res, object data, Response r)
		{
			st = (Stacktrace)res;
			if (st.usable())
				bug.similar(new Response(testSimilar,null,data));
			else
				bug.getValues("Status",new Response(testNeedinfo,null,data));
			bug.describe();	
		}

		private void testNeedinfo(object res, object data, Response r)
		{
			StringHash values = (StringHash)res;
			if (values["Status"]=="UNCONFIRMED")
				postEvent(new Event(BugEvent.BadStacktrace,bug,null, "Crap stacktrace?"));
			taskLock = false;
		}

		private Queue<Bug> dupe = null;

		private void testSimilar(object res, object data, Response r)
		{
			dupe = new Queue<Bug>((Bug[])res);
			if (!checkDupe()) // nothing to check
				bug.getValues("Status",new Response(testNeedinfoNoMatch,null,data));
		}

		private bool checkDupe()
		{
			if (dupe.Count == 0)
				return false;
			Bug b2 = dupe.Dequeue();
			if (bug.id == b2.id)
				return checkDupe();	
			else
				b2.getStacktrace(new Response(checkDupeStacktrace,null,b2));
			return true;	
		}

		private void checkDupeStacktrace(object res, object data, Response r)
		{
			Stacktrace st2 = (Stacktrace)res;
			Bug b2 = (Bug) data;
			if (st == st2)
			{
				postEvent(new Event(BugEvent.Duplicate,bug,b2,String.Format("{0} and {1} are duplicates?",bug.id,b2.id)));
				taskLock = false;
			}
			else
			{
				Console.WriteLine("{0} not a match for {1}",b2.id,bug.id);
				if (!checkDupe())
					bug.getValues("Status",new Response(testNeedinfoNoMatch,null,data));
			}
		}

		private void testNeedinfoNoMatch(object res, object data, Response r)
		{
			StringHash values = (StringHash)res;
			if (values["Status"]=="UNCONFIRMED")
			{
				st.print();
				postEvent(new Event(BugEvent.NoMatch,bug,null,"Can't find match. Need better trace?"));
			}
			else
				Console.WriteLine("Not unconfirmed, so not need better trace");
			taskLock = false;
		}

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
			if (this.dupl.bug!=null)
				postChange(new Delta(BugChange.MarkDupe,curr.bug,dupl.bug));
			else
				postChange(new Delta(BugChange.MarkBad,curr.bug,null));
			doing = false;
			notify.WakeupMain();
		}
		
		public void OnNoClicked(object o, EventArgs args)
		{
			doing = false;
			notify.WakeupMain();
		}

		protected void OnOpenUri (object o, OpenUriArgs args)
		{
			args.RetVal = true; /* don't load */
		}
	}
}
