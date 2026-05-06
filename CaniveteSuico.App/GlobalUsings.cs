// Resolve ambiguity between System.Windows.Application (WPF) and
// System.Windows.Forms.Application (Windows Forms) caused by UseWindowsForms=true.
global using WpfApplication = System.Windows.Application;
global using WinFormsApplication = System.Windows.Forms.Application;
