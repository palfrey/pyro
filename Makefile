Pyro.exe: PyroCore.dll MainWindow.cs gui.glade
	gmcs -pkg:gtk-sharp-2.0 -pkg:gecko-sharp-2.0 -pkg:glade-sharp-2.0 MainWindow.cs -r:PyroCore.dll -out:$@ -resource:gui.glade -debug -pkg:gtkhtml-sharp-2.0

PyroCore.dll: client.cs
	gmcs -out:$@ $^ -debug -target:library -r:System.Web -r:System.Data -r:Mono.Data.SqliteClient
