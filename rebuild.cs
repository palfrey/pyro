using Gtk;
using Pyro;
using System.IO;
using System;
using System.Collections.Generic;

class Rebuild
{
	static Queue <Bug> todo = new Queue<Bug>();
	static string cachepath = "cache";
	
	public static void Main(string[] args)
	{
		Bugzilla bugz = new Bugzilla("http://bugzilla.gnome.org/");
		BugDB.bugz = bugz;

		DirectoryInfo di = new DirectoryInfo(cachepath);

		foreach (int i in BugDB.DB.allBugs())
		{
			if (!File.Exists("cache/"+String.Concat(i)))
			{
				BugDB.DB.remove(i);
				Console.WriteLine("removing {0}",i);
			}
		}
		//throw new Exception();

		foreach(FileInfo f in di.GetFiles())
		{
			if (f.Name.IndexOf("-")!=-1)
				continue;
			try {
				Console.WriteLine("bug? {0}",f.Name);
				int id = Int32.Parse(f.Name);
				if (BugDB.DB.getExisting(id)!=null)
					continue;
				Console.WriteLine("Added bug {0}",id);
				Bug b = new Bug(id,bugz);
				todo.Enqueue(b);
				if (todo.Count>5)
					break;
			}
			catch (FormatException) {} // ignore. non-id files
		}
		//throw new Exception();
		nextBug(null,null,null);
		Application.Init();
		Application.Run();
	}

	public static void nextBug(object res, object input, Response r)
	{
		if (todo.Count!=0)
		{
			Bug bug = todo.Dequeue();
			//BugDB.DB.setExisting(bug.id);
			Console.WriteLine("Rebuilding bug {0}",bug.id);
			bug.buildBug(new Response(doStack,r,bug));
		}
	}

	public static void doStack(object res, object input, Response r)
	{
		Bug bug = (Bug)input;
		bug.getStacktrace(new Response(nextBug,r,bug));
	}
}
