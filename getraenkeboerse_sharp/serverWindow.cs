using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections;
using System.IO;
using Gtk;
using common;
using getraenkeboerse_sharp;
using System.Timers;

public partial class MainWindow : Gtk.Window
{	
	private ArrayList clients = new ArrayList();
	private Gtk.ListStore tvConnectionsModel = new ListStore(typeof(string));
	
	private System.Net.Sockets.Socket serverSocket; // socket to listen for new connections.
	
	private byte[] byteData = new byte[4096];
	
	// parameters for price manipulation
	private uint decreaseAmount = 1;
	private uint increaseAmount = 1;
	
	private string drinkFileName = "drinks.txt";
	
	Gtk.NodeStore store;
	Gtk.NodeStore DrinkStore {
		get {
			if (store == null) {
		        store = new Gtk.NodeStore (typeof (DrinkTreeNode));
			}
				return store;
			}
	}
	
	// TODO: resolve the following dailywtf-situation of having 2 hashtables!
	Hashtable clientsToRows = new Hashtable(); // clients connected them to corresponding treeview rows!
	Hashtable addressesToClients = new Hashtable(); // other way 'round
	
	// this stores all needed information about connected clients
	struct clientInformation {
		System.Net.Sockets.Socket client;
		Gtk.TreeIter treeIter;
		public clientInformation(System.Net.Sockets.Socket c, Gtk.TreeIter ti){
			client = c;
			treeIter = ti;
		}
	}
	Hashtable clientsToInfos = new Hashtable(40);
	
	public MainWindow () : base(Gtk.WindowType.Toplevel)
	{
		Build ();
		IPAddress[] a = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
		
		// visualizing clients
		txtIp.Text=a[0].ToString();
   		for (int i=0; i<a.Length; i++)
      		log (string.Format("IpAddr[{0}]={1}",i,a[i]),null);
		Gtk.TreeViewColumn tvColName = new Gtk.TreeViewColumn();
		tvColName.Title = "Address";
		tvConnections.AppendColumn(tvColName);
		tvConnections.Model = tvConnectionsModel;
		Gtk.CellRendererText adr = new Gtk.CellRendererText();
		tvColName.PackStart(adr, true);
		tvColName.AddAttribute(adr, "text", 0);
		
		// establishing the drink list
        tvDrinks.AppendColumn ("Drink", new Gtk.CellRendererText (), "text", 0);
		tvDrinks.AppendColumn ("Count", new Gtk.CellRendererText (), "text", 1);
		tvDrinks.AppendColumn ("Price Min", new Gtk.CellRendererText(), "text", 2);
		tvDrinks.AppendColumn ("Price Max", new Gtk.CellRendererText(), "text", 3);
		tvDrinks.AppendColumn ("Price", new Gtk.CellRendererText(), "text", 4);
		
  		tvDrinks.ShowAll ();	
		tvDrinks.NodeStore = DrinkStore;
		
		readDrinks();
	}

	protected void OnDeleteEvent (object sender, DeleteEventArgs a)
	{
		writeDrinks();
		Application.Quit ();
		a.RetVal = true;
	}
	
	protected virtual void OnBtnOkClicked (object sender, System.EventArgs e)
	{
		txtPort.IsEditable=false;
		lblState.Text="Server started.";
		startServer();
				
		btnCancel.Sensitive=true;
		btnOk.Sensitive=false;
	}
	
	private void readDrinks(){
		if (!File.Exists(drinkFileName))
		    return;
		TextReader tr = new StreamReader(drinkFileName);
		string line;
		while ((line = tr.ReadLine()) != null){
			// format is drink|minprice|maxprice|curprice (seperated by |)
			string[] info = line.Split('|');
			addDrink(info[0], uint.Parse(info[1]), uint.Parse(info[2]), uint.Parse(info[3]));
		}
		tr.Close();
	}
	
	private void writeDrinks(){
		TextWriter tw = new StreamWriter(drinkFileName);
		foreach (DrinkTreeNode d in DrinkStore){
			tw.WriteLine(d.DrinkName + "|" + d.MinPrice.ToString() + "|" + d.MaxPrice.ToString() + "|" + d.Price.ToString());
		}
		tw.Close();
	}
	
	protected virtual void OnBtnCancelClicked (object sender, System.EventArgs e)
	{
		txtPort.IsEditable=true;
		lblState.Text="Server stopped.";
				
		foreach (System.Net.Sockets.Socket c in clients){
			logoutClient(c);
		}
		
		btnCancel.Sensitive=false;
		btnOk.Sensitive=true;
	}
	
	public void startServer(){
		log("Starting server...", null);
		try {
			serverSocket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork,
			                          SocketType.Stream,
			                          ProtocolType.Tcp);
			IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 
			                                       int.Parse(txtPort.Text));
			serverSocket.Bind(ipEndPoint);
			serverSocket.Listen(4);
			serverSocket.BeginAccept(new AsyncCallback(OnAccept), null);
			
			System.Timers.Timer t = new System.Timers.Timer(double.Parse(txtInt.Text));
			t.Elapsed += new ElapsedEventHandler(sendDrinksToClients);
			t.Enabled = true;
			t.Start();
		}
		catch (Exception ex){
			log("Something went wrong during server initialization:");	
			log(ex.Message);
		}
	}
	
	private void sendDrinksToClients(object sender, EventArgs args){
		log ("Updating drinks!");
		foreach (System.Net.Sockets.Socket c in clients){
			log ("Sending drinks to client: " + clients.Count);
			if (c is System.Net.Sockets.Socket){
				log ("c is a socket");	
			}
			else {
				log ("c is no socket");	
			}
			sendDrinks(c);
		}
	}
	
	private void OnAccept(IAsyncResult ar){
		try {
			System.Net.Sockets.Socket clientSocket = serverSocket.EndAccept(ar);
			// listen for new clients!
			serverSocket.BeginAccept(new AsyncCallback(OnAccept), null);
			clientSocket.BeginReceive(byteData, 
			                          0, 
			                          byteData.Length, 
			                          SocketFlags.None, 
			                          new AsyncCallback(OnReceive),
			                          clientSocket);
		}
		catch (Exception ex){
			log("Something went wrong during accepting the client:");
			log(ex.Message);
		}
	}
	
	private void OnReceive(IAsyncResult ar){
		try {
			System.Net.Sockets.Socket clientSocket = 
				(System.Net.Sockets.Socket)ar.AsyncState;
			int bytecount = clientSocket.EndReceive(ar);
			
			// now we have the retrieved data in byteData
			Message incomingMsg = new Message(byteData, bytecount);
			
			// resume receiving incoming information
			clientSocket.BeginReceive(byteData, 
			                          0, 
			                          byteData.Length, 
			                          SocketFlags.None,
			                          new AsyncCallback(OnReceive),
			                          clientSocket);
			
			processIncomingMessage(incomingMsg, clientSocket);
		}
		catch (Exception ex){
			log("Something went wrong during receiving data:");
			log(ex.Message);
		}
	}
	
	private void processIncomingMessage(Message msg, System.Net.Sockets.Socket client){
		switch (msg.command){
		case Command.Login:
			log(string.Format("New client: {0}", msg.parameter));
			newClient(client, msg.parameter);
			break;
		case Command.Logout:
			log(string.Format("Client leaving: {0}", msg.parameter));
			logoutClient(client);
			break;
		case Command.Buy:
			//log("Someone bought something: " + msg.parameter);
			buyDrinks(msg.parameter);
			break;
		}
	}
	
	private void buyDrinks(string msg){
		log ("buyDrinks called with: " + msg);
		string[] drinkIndices = msg.Split(',');
		foreach (string d in drinkIndices){
			buyDrink(d);
		}
	}
	
	private void buyDrink(string drinkPath){
		log ("Someone bought drink with path: " + drinkPath);
		DrinkTreeNode n = (DrinkTreeNode)DrinkStore.GetNode(new TreePath(drinkPath));
		n.Count = n.Count + 1;
		// at the moment, we are using a very simple system for price calculation
		// TODO: check if this system is practicable
		uint numDrinks = 0;
		foreach (DrinkTreeNode d in DrinkStore){
			numDrinks++;
			if (d!=n){
				d.Price = d.Price - decreaseAmount;
			}
		}
		n.Price = n.Price + increaseAmount;// * numDrinks;
		tvDrinks.QueueDraw();
	}
			         
	private void newClient(System.Net.Sockets.Socket client, string name){
		IPEndPoint e = (IPEndPoint)client.RemoteEndPoint;
		string address = e.Address.ToString() + ":" + e.Port.ToString();
		log(name + " connected (" + address + ")");
		clients.Add(client);
		Gtk.TreeIter newrow;
		string display = name + "(" + address + ")";
		newrow = tvConnectionsModel.AppendValues(display);
		// install organisational stuff
		clientsToRows.Add(client, newrow);
		addressesToClients.Add(address, client);
		tvConnections.QueueDraw();
		sendDrinks(client);
	}
	
	private void sendDrinks(System.Net.Sockets.Socket client){
		// send drinks to client
		log("Constructing drink string:");
		log(getDrinksForMessage());
		Message drinks = new Message(Command.DescribeDrinks, getDrinksForMessage());
		byte[] bytes = drinks.toByte();
		client.BeginSend(bytes,
		                 0,
		                 bytes.Length,
		                 SocketFlags.None,
		                 new AsyncCallback(OnSend),
		                 client);
	}
	
	private string getDrinksForMessage(){
		string res = "";
		foreach (DrinkTreeNode d in DrinkStore){
			res = res + d.DrinkName;
			res = res + ",";
			res = res + d.Price.ToString();
			res = res + "|";
		}
		return res;
	}
	
	private void logoutClient(System.Net.Sockets.Socket client){
		client.Close();
		TreeIter ti = (TreeIter)clientsToRows[client];
		tvConnectionsModel.Remove(ref ti);
		tvConnections.QueueDraw();
	}
	
	// TODO: make this stuff dynamic/correct!
	protected virtual void OnBtnMessageClicked (object sender, System.EventArgs e)
	{
		// message stuff
		string message = "Msg from server!";
		byte[] data = System.Text.Encoding.ASCII.GetBytes(message);
		// get correct client!
		Gtk.TreeIter sel = new Gtk.TreeIter();
		TreeModel tm;
		tvConnections.Selection.GetSelected(out tm, out sel);
		string address = (string)(tm.GetValue(sel, 0));
		System.Net.Sockets.Socket client = (System.Net.Sockets.Socket)clients[0];
		Message msg = new Message(Command.DescribeDrinks, "drink1/drink2/drink3");
		byte[] bytes = msg.toByte();
		client.BeginSend(bytes,
		                 0,
		                 bytes.Length, 
		                 SocketFlags.None,
		                 new AsyncCallback(OnSend),
		                 client);
	}
	
	private void OnSend(IAsyncResult ar){
		try {
			System.Net.Sockets.Socket client = (System.Net.Sockets.Socket)ar.AsyncState;
			if (client==null){
				log ("Client is null!");	
			}
			client.EndSend(ar);
		}
		catch (Exception ex){
			log ("Something went wrong during sending message.");
			log (ex.Message);
		}
	}
	
	private void log(string msg){
		log(msg, null);	
	}
	
	private void log(string msg, TcpClient client){
		string append = "\n";
		if (client != null){
			IPEndPoint e = (IPEndPoint)client.Client.RemoteEndPoint;
			append = append + "[" + e.Address.ToString() + ":" + e.Port.ToString() + "]";
		}
		append = append + msg;
		//txtLog.Buffer.Insert(txtLog.Buffer.EndIter, append);
		Console.WriteLine(msg);
	}
	
	protected virtual void OnBtnDisconnectClicked (object sender, System.EventArgs e)
	{
		// get correct client
		Gtk.TreeIter sel = new Gtk.TreeIter();
		TreeModel tm;
		tvConnections.Selection.GetSelected(out tm, out sel);
		string address = (string)(tm.GetValue(sel, 0));
		TcpClient client = (TcpClient)addressesToClients[address];
		NetworkStream ns = client.GetStream();
		byte[] data = Encoding.ASCII.GetBytes("CLOSE");
		ns.Write(data, 0, data.Length);
	}
	
	protected virtual void OnBtnNewDrinkClicked (object sender, System.EventArgs e)
	{
		AddDrinkWindow ndw = new AddDrinkWindow();
		ndw.Modal = true;
		ndw.Run();
		addDrink(ndw.getDrinkName(), ndw.defaultprice, ndw.minprice, ndw.maxprice);
		ndw.Destroy();
	}
		
	private void addDrink(string name, uint def, uint min, uint max){
		DrinkStore.AddNode(new DrinkTreeNode(name, def, min, max));
	}
	
	[TreeNode (ListOnly=true)]
	public class DrinkTreeNode : Gtk.TreeNode {
		int count = 0;
		public DrinkTreeNode (string dn, uint c, uint minprice, uint maxprice)
		{
		    DrinkName = dn;
			MinPrice = minprice;
			MaxPrice = maxprice;
		    this.Price = c;
		}
		[Gtk.TreeNodeValue (Column=0)]
		public string DrinkName;
		[Gtk.TreeNodeValue (Column=1)]
		public int Count {get { return count; } set { count = value; }}
		[Gtk.TreeNodeValue (Column=2)]
		public uint MinPrice;
		[Gtk.TreeNodeValue (Column=3)]
		public uint MaxPrice;
		[Gtk.TreeNodeValue (Column=4)]
		public uint Price;
	}
}