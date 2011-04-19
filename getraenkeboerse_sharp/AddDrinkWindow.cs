using System;
namespace getraenkeboerse_sharp
{
	public partial class AddDrinkWindow : Gtk.Dialog
	{
		private string drinkName="";
		public uint minprice=0;
		public uint maxprice=10000;
		public uint defaultprice=350;
		
		protected virtual void OnButtonOkClicked (object sender, System.EventArgs e)
		{
			drinkName = txtDrinkName.Text;
			minprice = uint.Parse(txtMin.Text);
			maxprice = uint.Parse(txtMax.Text);
			defaultprice = uint.Parse(txtDefault.Text);
			this.Hide();
		}
		
		
		public AddDrinkWindow ()
		{
			this.Build ();
		}
		
		public string getDrinkName(){
			return drinkName;
		}
		
		protected virtual void OnResponse (object o, Gtk.ResponseArgs args)
		{
			
		}
		
		
	}
}

