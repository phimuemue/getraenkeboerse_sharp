using System;
using Gtk;

namespace getraenkeboerse_kasse
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Console.WriteLine("Getraenkeboerse Kasse");
			Application.Init ();
			MainWindow win = new MainWindow ();
			win.Show ();
			Application.Run ();
		}
	}
}
