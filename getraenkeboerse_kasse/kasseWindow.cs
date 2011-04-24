using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using Gtk;
using common;
using getraenkeboerse_widgetlibrary;
using System.Collections;

// TODO: encapsulate all beginsend's in a single message-method

public partial class MainWindow : Gtk.Window
{	
	private System.Net.Sockets.Socket clientSocket;
	private string strName;
	
	private byte[] byteData = new byte[2048];
	
	private uint numDrinks = 0;
	
	private bool drinksInitialized = false;
	private ArrayList drinkOrderers = new ArrayList();
	
	private Gdk.Key[] rawOrderingKeys = {
		Gdk.Key.F1,
		Gdk.Key.F2,
		Gdk.Key.F3,
		Gdk.Key.F4,
		Gdk.Key.F5,
		Gdk.Key.F6,
		Gdk.Key.F7,
		Gdk.Key.F8,
		Gdk.Key.F9,
		Gdk.Key.F10,
		Gdk.Key.F11,
		Gdk.Key.F12
	};
	// this beast stores the keys and associated drinkOrderers to get drinkorderers from key presses
	private Hashtable usedOrderingKeys = new Hashtable(24);
	
	
	Gtk.NodeStore orderStore;
	Gtk.NodeStore OrderStore {
		get {
			if (orderStore==null){
				orderStore = new Gtk.NodeStore(typeof(DrinkOrder));	
			}
			return orderStore;
		}
	}
	
	
	public MainWindow () : base(Gtk.WindowType.Toplevel)
	{
		Build ();
		txtName.Text = "Kasse " + (new Random()).Next((Int32)1000).ToString();
		// Install order list.
		tvOrders.AppendColumn("Idx", new Gtk.CellRendererText(), "text", 0);
		tvOrders.AppendColumn("Drink", new Gtk.CellRendererText(), "text", 1);
		tvOrders.AppendColumn("Price", new Gtk.CellRendererText(), "text", 2);
		tvOrders.ShowAll();
		tvOrders.NodeStore = OrderStore;
		
		this.Default = btnOrder;
	}

	protected void OnDeleteEvent (object sender, DeleteEventArgs a)
	{
		Gtk.Application.Quit ();
		a.RetVal = true;
	}
	
	protected virtual void OnBtnConnectClicked (object sender, System.EventArgs e)
	{
		try {
			clientSocket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork,
			                                             SocketType.Stream,
			                                             ProtocolType.Tcp);
			IPAddress ipAddress = IPAddress.Parse(txtIp.Text);
			log (txtIp.Text);
			IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, 
			                                       int.Parse(txtPort.Text));
			clientSocket.BeginConnect(ipEndPoint,
			                          new AsyncCallback(OnConnect),
			                          null);
			btnConnect.Sensitive = false;
			btnDisconnect.Sensitive = true;
			btnMsg.Sensitive = true;
		}
		catch (Exception ex) {
			log("Not able to connect to server:");
			log(ex.Message);
			btnConnect.Sensitive = true;
			btnDisconnect.Sensitive = false;
			btnMsg.Sensitive = false;
		}
	}
	
	private void OnConnect(IAsyncResult ar){
		try {
			clientSocket.EndConnect(ar); 
			// here we are connected, so we send login request!
			byte[] bytesMsg;
			Message loginMsg = new Message(Command.Login, txtName.Text);
			bytesMsg = loginMsg.toByte();
			
			clientSocket.BeginSend(bytesMsg, 
			                       0, 
			                       bytesMsg.Length, 
			                       SocketFlags.None,
			                       new AsyncCallback(OnSend),
			                       null);
			clientSocket.BeginReceive(byteData,
			                          0,
			                          byteData.Length,
			                          SocketFlags.None,
			                          new AsyncCallback(OnReceive),
			                          null);
		}
		catch (Exception ex){
			log ("Something went wrong during establishing connection:");
			log (ex.Message);
			btnConnect.Sensitive = true;
			btnDisconnect.Sensitive = false;
			btnMsg.Sensitive = false;
		}
	}
	
	private void OnSend(IAsyncResult ar){
		try {
			clientSocket.EndSend(ar);
		}
		catch (ObjectDisposedException){ }
		catch (Exception ex) {
			log ("Something went wrong during sending:");
			log (ex.Message);
		}
	}
	
	private void OnReceive(IAsyncResult ar){
		try {
			int bytecount = clientSocket.EndReceive(ar);
			// decode the thing into a message
			Message msg = new Message(byteData, bytecount);
			switch (msg.command){
			case Command.DescribeDrinks:
				// log ("somestuff"); // for some reason, this doesn't work.
				//log(msg.parameter);
				updateDrinksFromServer(msg.parameter);
				break;
			default:
				log ("Unknown message type");
				break;
			}
			clientSocket.BeginReceive(byteData,
			                          0,
			                          byteData.Length,
			                          SocketFlags.None,
			                          new AsyncCallback(OnReceive),
			                          null);
		}
		catch (ObjectDisposedException) {
			log ("ObjectDisposedException");
		}
		catch (Exception ex){
			log("Something went wrong during recieving:");
			log(ex.Message);
		}
	}
	
	private void updateDrinksFromServer(string msg){
		if (drinksInitialized){
			// TODO: Add some kind of information that new prices have been sent
			Gtk.MessageDialog msgDialog = new Gtk.MessageDialog(this, 
				                                                Gtk.DialogFlags.DestroyWithParent,
				                                                Gtk.MessageType.Info,
				                                                Gtk.ButtonsType.Close,
				                                                "New prices adapted.");
		}
		string[] drinks = msg.Split('|');
		int i=0;
		foreach (string d in drinks){
			string[] tmp=d.Split(',');
			if (tmp.Length>1){
				// is this the initial drink information ...
				if (!drinksInitialized){
					addDrink(tmp[0], int.Parse(tmp[1]));
				}
				// ... or have drinks already been received?
				else {
					((getraenkeboerse_widgetlibrary.drinkorderer)drinkOrderers[i]).Price = int.Parse(tmp[1]);
				}
			}
			i++;
		}
		drinksInitialized = true;
	}
	
	protected virtual void OnBtnMsgClicked (object sender, System.EventArgs e)
	{
		byte[] tosend;
		Message buyMsg = new Message(Command.Buy, (new Random()).Next(3).ToString());
		tosend = buyMsg.toByte();
		clientSocket.BeginSend(tosend,
		                       0,
		                       tosend.Length,
		                       SocketFlags.None,
		                       new AsyncCallback(OnSend),
		                       null);
	}
	
	public void ReadIncomingData(){
		
	}
	
	protected virtual void OnBtnDisconnectClicked (object sender, System.EventArgs e)
	{
		disconnectMe();
	}
	
	private void disconnectMe(){
		try {
			Message logoutMsg = new Message(Command.Logout, txtName.Text);
			byte[] bytes = logoutMsg.toByte();
			clientSocket.Send(bytes,
			                  0, 
			                  bytes.Length, 
			                  SocketFlags.None);
			clientSocket.Close();
			btnConnect.Sensitive = true;
			btnMsg.Sensitive = false;
			btnDisconnect.Sensitive = false;
		} 
		catch (ObjectDisposedException){ }
		catch (Exception ex){
			log ("There occured an error during disconnecting.");
			log (ex.Message);
		}
		
	}
	
	protected virtual void OnDestroyEvent (object o, Gtk.DestroyEventArgs args)
	{
	}
	
	private void log(string msg){
		Console.WriteLine(msg);
		//TextIter ti = txtLog.Buffer.EndIter;
		//txtLog.Buffer.Text = txtLog.Buffer.Text + "\n" + msg;
	}
	
	// The following adds controls to order a drink!
	private void addDrink(string dname, int price){
		numDrinks++;
		getraenkeboerse_widgetlibrary.drinkorderer dro = new getraenkeboerse_widgetlibrary.drinkorderer();
		dro.Show();
		dro.DrinkName = dname;
		dro.Price = price;
		tblDrinkOrderers.Attach(dro,
		                        numDrinks,
		                        numDrinks+1,
		                        0,
		                        1);
		tblDrinkOrderers.Show();
		dro.BuyAction += triggerBuyAction;
		dro.DrinkIndex = numDrinks-1;
		drinkOrderers.Add(dro);
		if (numDrinks<12){
			usedOrderingKeys.Add(rawOrderingKeys[numDrinks-1], dro);
			dro.BuyCaption = rawOrderingKeys[numDrinks-1].ToString();
		}
	}
	
	private void triggerBuyAction(object obj, EventArgs args){
		drinkorderer dro = (drinkorderer)obj;
		log ("Someone bought a " + dro.DrinkName + "(" + dro.DrinkIndex.ToString() + ")");
		OrderStore.AddNode(new DrinkOrder(dro.DrinkIndex, dro.DrinkName, dro.Price));
		log ("buying done!");
		// calculate price
		int p = 0; // price
		foreach (DrinkOrder d in orderStore){
			p += d.price;	
		}
		btnOrder.Label = string.Format("{0:0.00} Euro", p/100.0);
	}
	
	private void OrderStuff(object obj, EventArgs args){
		// TODO: correct this to send useful stuff to server.
		//messageToServer(Command.Buy, dro.DrinkIndex.ToString());
	}
	
	private void messageToServer(Command c, string p){
		Message msg = new Message(c, p);
		byte[] bytes = msg.toByte();
		clientSocket.BeginSend(bytes,
		                       0,
		                       bytes.Length,
		                       SocketFlags.None,
		                       new AsyncCallback(OnSend),
		                       null);
	}
	
	protected virtual void OnBtnOrderClicked (object sender, System.EventArgs e)
	{
		// we just store all indices of drinks and send them as string
		StringBuilder sb = new StringBuilder("", 0, 0, 40);
		foreach (DrinkOrder d in OrderStore){
			sb.Append(d.drinkIndex);
			sb.Append(",");
		}
		log (sb.ToString());
		messageToServer(Command.Buy, sb.ToString());
		orderStore.Clear();
		btnOrder.Label = "0 Euro";
	}
	
	protected virtual void OnKeyPressEvent (object o, Gtk.KeyPressEventArgs args)
	{
		Gdk.Key k = args.Event.Key;
		if (usedOrderingKeys.Contains (k)){
			triggerBuyAction(usedOrderingKeys[k], null);
			btnOrder.GrabFocus();
		}
		log (k.ToString());
	}
	
}

[TreeNode (ListOnly=true)]
public class DrinkOrder : Gtk.TreeNode
{
	public DrinkOrder(uint i, string n, int p){
		drinkIndex = i;
		name = n;
		price = p;
	}
	[Gtk.TreeNodeValue (Column=0)]
	public uint drinkIndex;
	[Gtk.TreeNodeValue (Column=1)]
	public string name;
	[Gtk.TreeNodeValue (Column=2)]
	public int price;
}

public class StateObject
{
	// Client  socket.
	public System.Net.Sockets.Socket workSocket = null;
	// Size of receive buffer.
	public const int BufferSize = 1024;
	// Receive buffer.
	public byte[] buffer = new byte[BufferSize];
	// Received data string.
	public StringBuilder sb = new StringBuilder();
}
