using Gtk;
using Pyro;
using System.IO;
using System;
using System.Collections.Generic;

public class GetDupe
{
	public static void Main(string[] args)
	{
		Application.Init();
		new GetDupe(int.Parse(args[0]));
		Application.Run();
	}

	Bug bug;
	
	GetDupe(int id)
	{
		Bugzilla bugz = new Bugzilla("http://bugzilla.gnome.org/");
		BugDB.bugz = bugz;
		bug = new Bug(id,bugz);
		bug.getStacktrace(new Response(grabStacktrace,null,bug));
	}
	
	private void grabStacktrace(object res, object data, Response r)
	{
		Stacktrace st = (Stacktrace)res;
		st.print();
		if (st.usable())
		{
			BugDB.DB.similar(bug.id,new Response(moreStacktraces,r, data));	
		}
		else
			Console.WriteLine("stacktrace isn't usable");
	}
	
	private void moreStacktraces(object res, object data, Response r)
	{
		Console.WriteLine("moreStackTraces: {0}",res);
		if (res!=null)
		{
			Bug b2 = (Bug)res;
			Console.WriteLine("moreStackTraces: res not null!",res);
			Console.WriteLine("other bug is {0}, hash is {1}",b2.id,b2.stackhash);
		}
		else
			bug.similar(new Response(grabSimilar,r,data));
	}

	private void grabSimilar(object res, object data, Response r)
	{
		Bug[] bugs = (Bug[])res;
		foreach(Bug b in bugs)
		{
			Console.WriteLine("shown {0}",b.id);
		}
	}
}
