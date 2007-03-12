using Glade;
using Gtk;
using System;
using System.IO;
using Pyro;
using System.Collections;
using System.Threading;
using System.Collections.Generic;
using System.Net;
using System.Xml.Xsl;

namespace PyroGui
{
	class BugDisplay
	{
		//public WebControl web;
		public HTML web;
		public Bug bug;

		public BugDisplay(Frame frm)
		{
			//web = new WebControl();
			ScrolledWindow sw = new ScrolledWindow();
			web = new HTML();
			web.Show();
			sw.Add(web);
			//web.StatusChange += new EventHandler(changeHandler);
			//web.NetStart += new EventHandler(NetStateAllHandler);
			frm.Add(sw);
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
			XslTransform transform = new XslTransform();
			transform.Load("bugs.xsl");
			transform.Transform(bug.localpath(),bug.localpath()+"-trans");

			TextReader inFile = new StreamReader(bug.localpath()+"-trans");
			string ret = inFile.ReadToEnd();
			inFile.Close();
			ret = ret.Replace("&lt;signal handler called&gt;","<a name=\"stacktrace\"><font color=\"#00FF00\">&lt;signal handler called&gt;</font>");
			web.LoadFromString(ret);
			web.JumpToAnchor(stacktrace?"stacktrace":"c0");
		}
		
		/*public void loadURL(string url)
		{
			web.LoadUrl(url);
		}*/
		
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
			web.LoadEmpty();
		}
	}
	
	public class MainWindow
	{
		BugDisplay curr,dupl;
		[Widget] Frame frmCurrent;
		[Widget] Frame frmDupl;
		[Widget] Label lblStatus;
		[Widget] Gnome.HRef hrfBrowser;

		[Widget] Dialog dlgLogin;
		[Widget] Entry entUsername;
		[Widget] Entry entPassword;

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
			RanOut
		}

		bool didranout = false;

		public enum BugChange
		{
			MarkBad,
			MarkDupe,
			MarkDone
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
	
		Glade.XML gxml;

		bool hasprocess = false;

		string product = null;
		
		public MainWindow(string[] Args)
		{
			gxml = new Glade.XML(null, "gui.glade", "MainWindow", null);
			gxml.Autoconnect(this);

			events = new Queue<Event>();
			deltas = new Queue<Delta>();
			
			curr = new BugDisplay(frmCurrent);
			//curr.render(false,"hello world");
			dupl = new BugDisplay(frmDupl);

			((Window)gxml.GetWidget("MainWindow")).Maximize();
			((Window)gxml.GetWidget("MainWindow")).ShowAll();
			hrfBrowser.Clicked += OnNoClicked;
			//GlobalProxySelection.Select = new WebProxy("http://taz:8118");
			
			bugz = new Bugzilla("http://bugzilla.gnome.org/");
			BugDB.bugz = bugz;
			if (Args.Length !=0)
				product = Args[0];

			todo = new Queue<Bug>();
			ready();
			hasprocess = true;
			GLib.Idle.Add(new GLib.IdleHandler(processTask));
			notify = new ThreadNotify (new ReadyEvent (ready));
		}

		void ready ()
		{
			if (doing)
				return;
			if (events.Count>0)
			{
				Console.WriteLine("grab event");
				Event e = events.Dequeue();
				lblStatus.Text = e.text;
				this.dupl.bug = null;
				this.dupl.clear();
				switch(e.r)
				{
					case BugEvent.LoginFailure:
						doing = true;
						break;
					case BugEvent.LoginSuccess:
						break;
					case BugEvent.Duplicate:
						Console.WriteLine("dupl: {0}",e.dup.id);
						this.dupl.bug = e.dup;
						this.dupl.showBug(true);
						goto case BugEvent.BadStacktrace;
					case BugEvent.NoMatch:
					case BugEvent.BadStacktrace:
						Console.WriteLine("curr: {0}",e.b.id);
						this.curr.bug = e.b;
						this.curr.showBug(e.r==BugEvent.NoMatch || this.dupl.bug!=null);
						((Window)gxml.GetWidget("MainWindow")).Title = "Pyro (*)";
						hrfBrowser.Url = "http://bugzilla.gnome.org/show_bug.cgi?id="+this.curr.bug.id;
						doing = true;
						break;
					case BugEvent.RanOut:
						this.curr.clear();
						if (events.Count!=0)
						{
							events.Enqueue(e);
							lblStatus.Text = "dunno";
						}
						((Window)gxml.GetWidget("MainWindow")).Title = "Pyro (Done)";
						if (!hasprocess && deltas.Count!=0)
						{
							hasprocess = true;
							GLib.Idle.Add(new GLib.IdleHandler(processTask));
						}
						break;
					default:
						break;
				}
				Console.WriteLine("grab event done");
				if (!taskLock)
					endTask();
			}
			else
			{
				lblStatus.Text = "Looking for new events...";
				((Window)gxml.GetWidget("MainWindow")).Title = "Pyro";
				this.curr.clear();
				this.dupl.clear();
			}
		}

		private void postEvent(Event e)
		{
			Console.WriteLine("\npostEvent {0}\n",e.r);
			events.Enqueue(e);
			if (!doing)
			{
				Console.WriteLine("notify\n");
				notify.WakeupMain();
			}
		}

		private void postChange(Delta d)
		{
			Console.WriteLine("Change: {0}, id: {1}",d.c,d.b.id);
			deltas.Enqueue(d);
			if (!doing)
			{
				Console.WriteLine("\nnotify\n");
				notify.WakeupMain();
			}
			if (!hasprocess)
			{
				hasprocess = true;
				GLib.Idle.Add(new GLib.IdleHandler(processTask));
			}
		}

		bool taskLock = false;

		bool didcorebugs = false;
		
		public bool processTask()
		{
			if (taskLock)
			{
				hasprocess = false;
				return false;
			}
			if (!bugz.loggedIn)
			{
				Glade.XML gxml2 = new Glade.XML(null, "gui.glade", "dlgLogin", null);
				gxml2.Autoconnect(this);
				dlgLogin.Modal = true;
				ResponseType resp = (ResponseType)dlgLogin.Run();
				dlgLogin.Hide();
				switch(resp)
				{
					case ResponseType.Ok:
						Console.WriteLine("ok");
						break;
					case ResponseType.None:	
					case ResponseType.Cancel:
					case ResponseType.DeleteEvent:
						Console.WriteLine("cancel");
						postEvent(new Event(BugEvent.LoginFailure,"Didn't login"));
						return false;
					default:
						Console.WriteLine("id={0}",resp);
						throw new Exception();
				}
				try
				{
					if (!bugz.login(entUsername.Text,entPassword.Text))
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
					case BugChange.MarkDone:
						BugDB.DB.setDone(d.b.id);
						endTask();
						break;
					default:
						throw new Exception();
				}
				d.b.clearRaw();
				return true;
			}
			if (events.Count<20)
			{
				taskLock = true;
				if (todo.Count == 0)
				{
					if (!didcorebugs)
					{
						didcorebugs = true;
						Console.WriteLine("\nlooking for bugs\n");
						if (product==null)
							new Bug(0,bugz).corebugs(new Response(extraBugs));
						else
						{
							try
							{
								int id = Int32.Parse(product);
								//bugz.getBug(int[]{new Bug(id,bugz)},new Response(extraBugs));
								new Bug(0,bugz).numbered(id,id+1,new Response(extraBugs));
							}
							catch (FormatException)
							{
								new Bug(0,bugz).product(product,new Response(extraBugs));
							}
						}
					}
					else
					{
						if (!didranout)
						{
							didranout = true;
							postEvent(new Event(BugEvent.RanOut,null,null,"Ran out of bugs!"));
						}
							
						endTask();
						hasprocess = false;
						return false;
					}
				}
				else
					nextBug(null,null,null);
			}
			return true;
		}

		private void endTask() {endTask(null,null,null);}
		private void endTask(object res, object data, Response r)
		{
			taskLock = false;
			if (!hasprocess && (!didranout || deltas.Count>0))
			{
				hasprocess = true;
				GLib.Idle.Add(new GLib.IdleHandler(processTask));
			}
			if (!doing)
				notify.WakeupMain();
			Response.invoke(r,null);	
		}

		private void extraBugs(object res, object data, Response r)
		{
			Bug []bugs = (Bug[])res;
			List<int> ids = new List<int>();
			foreach(Bug b in bugs)
			{
				if (!BugDB.DB.done(b.id))
				{
					todo.Enqueue(b);
					ids.Add(b.id);
				}
				else
					Console.WriteLine("{0} is marked as done",b.id);
			}
			bugz.getBug(ids.ToArray(),new VoidResponse(nextBug,r));
			//nextBug(r);
		}

		private Bug bug = null;
		private Stacktrace st = null;

		private void nextBug(object res, object input, Response r)
		{
			if (todo.Count==0)
			{
				if (!didranout)
				{
					didranout = true;
					postEvent(new Event(BugEvent.RanOut,null,null,"Ran out of bugs!"));
				}
			}
			else
			{
				bug = todo.Dequeue();
				bug.triageable(new Response(nextTriageable,r));
			}
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
				BugDB.DB.setDone(bug.id);
				bug.describe();
				endTask();
			}
			Response.invoke(r,null);
		}

		private void grabStacktrace(object res, object data, Response r)
		{
			st = (Stacktrace)res;
			if (st.usable())
				BugDB.DB.similar(bug.id,new Response(moreStacktraces,r, data));	
			else
				bug.getValues("Status",new Response(testNeedinfo,r,data));
			bug.describe();	
		}

		private void moreStacktraces(object res, object data, Response r)
		{
			if (res!=null)
			{
				Bug b2 = (Bug)res;
				postEvent(new Event(BugEvent.Duplicate,bug,b2,String.Format("{0} and {1} are duplicates?",bug.id,b2.id)));
				endTask();
				Response.invoke(r,null);
			}
			else
				bug.similar(new Response(testSimilar,r,data));
		}

		private void testNeedinfo(object res, object data, Response r)
		{
			StringHash values = (StringHash)res;
			if (values["Status"]=="UNCONFIRMED")
				postEvent(new Event(BugEvent.BadStacktrace,bug,null, "Crap stacktrace?"));
			endTask();	
			Response.invoke(r,null);
		}

		private Queue<Bug> dupe = null;

		private void testSimilar(object res, object data, Response r)
		{
			dupe = new Queue<Bug>((Bug[])res);
			if (!checkDupe(r)) // nothing to check
				bug.getValues("Status",new Response(testNeedinfoNoMatch,r,data));
		}

		private bool checkDupe(Response r)
		{
			if (dupe.Count == 0)
				return false;
			Bug b2 = dupe.Dequeue();
			if (bug.id <= b2.id)
				return checkDupe(r);	
			else
				b2.getStacktrace(new Response(checkDupeStacktrace,r,b2));
			return true;	
		}

		private void checkDupeStacktrace(object res, object data, Response r)
		{
			Stacktrace st2 = (Stacktrace)res;
			Bug b2 = (Bug) data;
			if (st == st2)
			{
				postEvent(new Event(BugEvent.Duplicate,bug,b2,String.Format("{0} and {1} are duplicates?",bug.id,b2.id)));
				endTask();
			}
			else
			{
				Console.WriteLine("{0} not a match for {1}",b2.id,bug.id);
				if (!checkDupe(r))
					bug.getValues("Status",new Response(testNeedinfoNoMatch,r,data));
			}
		}

		private void testNeedinfoNoMatch(object res, object data, Response r)
		{
			StringHash values = (StringHash)res;
			if (values["Status"]=="UNCONFIRMED")
			{
				st.print();
				postEvent(new Event(BugEvent.NoMatch,bug,null,String.Format("Can't find match. Need better trace for {0}?",bug.id)));
			}
			else
				Console.WriteLine("Not unconfirmed, so not need better trace");
			endTask();
			Response.invoke(r,null);
		}

		public static void Main(string[] args)
		{
			Gdk.Threads.Init();
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
			postChange(new Delta(BugChange.MarkDone,curr.bug,null));
			doing = false;
			notify.WakeupMain();
		}

		/*
		protected void OnOpenUri (object o, OpenUriArgs args)
		{
			args.RetVal = true;
		}*/
	}
}
