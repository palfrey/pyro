all:Pyro.exe

Pyro.exe: PyroCore.dll MainWindow.cs gui.glade NonValidating.dll
	gmcs -pkg:gtk-sharp-2.0 -pkg:glade-sharp-2.0 MainWindow.cs -r:PyroCore.dll -out:$@ -resource:gui.glade -debug -pkg:gnome-sharp-2.0 -pkg:gtkhtml-sharp-2.0 -r:NonValidating.dll

PyroCore.dll: client.cs NonValidating.dll SafeStringReader.cs
	gmcs -out:$@ client.cs -debug -target:library -r:System.Web -r:System.Data -r:Mono.Data.SqliteClient -r:NonValidating.dll SafeStringReader.cs

rebuild.exe: PyroCore.dll rebuild.cs
	gmcs rebuild.cs -out:$@ -debug -r:PyroCore.dll -pkg:gtk-sharp-2.0
	
NonValidating.dll: NonValidatingReader.cs
	gmcs -out:$@ $^ -debug -target:library

test.exe: test.cs NonValidating.dll
	gmcs test.cs -out:$@ -debug -r:NonValidating.dll

stack.exe: stacktest.cs PyroCore.dll
	gmcs -out:$@ -debug -r:PyroCore.dll stacktest.cs
