using System;
using System.Text;

namespace common
{
	public class Message
	{
		public Command command;
		public string parameter;
		
		public Message (Command c, string ai)
		{
			command = c;
			parameter = ai;
		}
		
		public Message (byte[] bytes){
			// TODO: is it possible to avoid copying between byte[]'s?
			command = (Command)bytes[0];
			byte[] bParameter = new byte[bytes.Length-1];
			for (int i=0; i<bParameter.Length-1; ++i){
				bParameter[i] = bytes[i+1];
			}
			parameter = Encoding.ASCII.GetString(bParameter, 0, bParameter.Length);
		}
		
		public Message (byte[] bytes, int bytecount){
			// TODO: is it possible to avoid copying between byte[]'s?
			command = (Command)bytes[0];
			byte[] bParameter = new byte[bytecount];
			for (int i=0; i<bParameter.Length-1; ++i){
				bParameter[i] = bytes[i+1];
			}
			parameter = Encoding.ASCII.GetString(bParameter, 0, bParameter.Length);
		}
		
		public byte[] toByte(){
			// TODO: is it somehow possible to avoid copying between byte[]'s?
			byte[] tmp = Encoding.ASCII.GetBytes(parameter);
			byte[] res = new byte[tmp.Length + 1];
			for (int i=0; i<tmp.Length; ++i){
				res[i+1] = tmp[i];	
			}
			res[0] = (byte)command;
			return res;
		}
	}
}

