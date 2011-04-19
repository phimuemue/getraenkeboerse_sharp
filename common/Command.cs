using System;
namespace common
{
	public enum Command : byte
	{
		// Client logging in
		Login = 1,
		// Client buys something
		Buy,
		// Client logging out
		Logout,
		// Server sending drink information
		DescribeDrinks,
		// Server setting a countdown
		CountDown
	}
}

