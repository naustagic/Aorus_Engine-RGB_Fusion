#define DEBUG
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace MontBlanc;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		Debug.WriteLine("main...");
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		Writer writer = new Writer();
		if (writer.isTinyProgramer())
		{
			Debug.WriteLine("TinyProgramer");
			TinyProgrammer mainForm = new TinyProgrammer();
			Application.Run(mainForm);
		}
		else
		{
			Debug.WriteLine("Form1 run...");
			Application.Run(new Form1());
		}
	}
}
