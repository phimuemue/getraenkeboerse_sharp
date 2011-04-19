using System;
using Gtk;

namespace getraenkeboerse_sharp
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Console.WriteLine("Getraenkeboerse Server");
			Application.Init ();
			MainWindow win = new MainWindow ();
			win.Show ();
			Application.Run ();
		}
	}
}
