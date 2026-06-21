using System.Windows;
using Microsoft.Win32;
using DStockAnalysis.ViewModels;

namespace DStockAnalysis;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainViewModel vm)
        {
            // CSV選択ダイアログを ViewModel に注入(View 層の責務)。
            vm.FilePicker = () =>
            {
                var dlg = new OpenFileDialog
                {
                    Title = "銘柄CSVを選択",
                    Filter = "CSVファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*",
                    CheckFileExists = true
                };
                return dlg.ShowDialog() == true ? dlg.FileName : null;
            };
        }
    }
}
