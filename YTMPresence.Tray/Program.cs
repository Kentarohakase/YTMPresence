using System;

namespace YTMPresence;

internal static class Program
{
  [STAThread]
  public static void Main()
  {
    var app = new YTMPresence.TrayWpf.App();
    app.InitializeComponent();
    app.Run();
  }
}
