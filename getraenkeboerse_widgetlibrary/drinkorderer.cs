
using System;

namespace getraenkeboerse_widgetlibrary
{


	[System.ComponentModel.ToolboxItem(true)]
	public partial class drinkorderer : Gtk.Bin
	{

		public drinkorderer ()
		{
			this.Build ();
		}

		public event EventHandler BuyAction;
		
		private int price;
		public int Price {
			get {
				return price;
			}
			set {
				lblPrice.Text = value.ToString();
				price = value;
			}
		}
		
		private string drinkName;
		public string DrinkName {
			get {
				return drinkName;
			}
			set {
				lblName.Text = value;
				drinkName = value;
			}
		}
		
		protected virtual void OnBtnBuyClicked (object sender, System.EventArgs e)
		{
			if (BuyAction!=null){
				BuyAction(this, e);
			}
		}
		
		private uint drinkindex;
		public uint DrinkIndex {
			get {
				return drinkindex;	
			}
			set {
				drinkindex = value;	
			}
		}
		
		private string buyCaption;
		public string BuyCaption {
			get {
				return buyCaption;	
			}
			set {
				btnBuy.Label = value;
				buyCaption = value;	
			}
		}
		
	}
}
