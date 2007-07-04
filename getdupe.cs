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
			BugDB.DB.similar(bug.id,new Response(moreStacktraces,r, data));	
	}
	
	private void moreStacktraces(object res, object data, Response r)
	{
		Console.WriteLine("moreStackTraces: {0}",res);
		if (res!=null)
			Console.WriteLine("moreStackTraces: res not null!",res);
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
