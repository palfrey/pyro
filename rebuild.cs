using Pyro;
using System.IO;
using System;
using System.Collections.Generic;

class Rebuild
{
	static Queue <Bug> todo = new Queue<Bug>();
	static string cachepath = "cache";
	
	private static string path(string cache)
	{
		return Path.GetFullPath(Path.Combine(cachepath,cache));
	}

	private static string readData(string cache)
	{
		StreamReader inFile = new StreamReader(path(cache));
		string ret = inFile.ReadToEnd();
		inFile.Close();
		return ret;
	}

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
		throw new Exception();

		foreach(FileInfo f in di.GetFiles())
		{
			if (f.Name.IndexOf("-")!=-1)
				continue;
			try {
				int id = Int32.Parse(f.Name);
				if (BugDB.DB.getExisting(id)!=null)
					continue;
				Bug b = new Bug(id,bugz);
				todo.Enqueue(b);
				StringHash[] orig = Bug.xmlParser(readData(String.Concat(id)),"bug");
				if (orig.Length == 0)
				{
					Console.WriteLine("{0} is malformatted, removing",id);
					File.Delete(path(String.Concat(id)));
					BugDB.DB.remove(id);
				}
			}
			catch (FormatException) {} // ignore. non-id files
		}
		nextBug(null,null,null);
	}

	public static void nextBug(object res, object input, Response r)
	{
		if (res!=null)
		{
			Stacktrace st = (Stacktrace)res;
			Bug curr = (Bug)input;
			curr.setStackHash(st);
		}
		if (todo.Count!=0)
		{
			Bug bug = todo.Dequeue();
			bug.getValues(null,new Response(doStack,r,bug));
		}
	}

	public static void doStack(object res, object input, Response r)
	{
		Bug bug = (Bug)input;
		bug.getStacktrace(new Response(nextBug,r,bug));
	}
}
