PyroGUI.exe: Pyro.dll MainWindow.cs gui.glade
	mcs -pkg:gtk-sharp-2.0 -pkg:gecko-sharp-2.0 -pkg:glade-sharp-2.0 MainWindow.cs -r:Pyro.dll -out:$@ -resource:gui.glade -debug

Pyro.dll: client.cs
	mcs -out:$@ $^ -debug -target:library
