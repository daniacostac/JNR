// File: Views/About.xaml.cs
using System;
using System.Diagnostics;
using System.Reflection; // For Assembly version
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation; // For RequestNavigateEventArgs

namespace JNR.Views
{
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
            LoadVersionInfo();
            // Update copyright year dynamically
            CopyrightTextBlock.Text = $"© {DateTime.Now.Year} [Your Name/Alias Here - Update This!]. All rights reserved.";
        }

        private void LoadVersionInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                if (version != null)
                {
                    VersionTextBlock.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
                }
                else
                {
                    VersionTextBlock.Text = "Version N/A";
                }
            }
            catch (Exception ex)
            {
                VersionTextBlock.Text = "Version Error";
                Debug.WriteLine($"Error loading version info: {ex.Message}");
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not open hyperlink: {ex.Message}");
                MessageBox.Show($"Could not open link: {e.Uri.AbsoluteUri}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            e.Handled = true;
        }

        private void SidebarNavigation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.CommandParameter is string viewName)
            {
                if (viewName == "About") // Current view
                {
                    rb.IsChecked = true; // Ensure it stays checked
                    return;
                }

                Window newWindow = null;
                switch (viewName)
                {
                    case "MyAlbums":
                        newWindow = new JNR.Views.My_Albums.MyAlbums();
                        break;
                    case "Genres":
                        newWindow = new JNR.Views.Genres.Genres();
                        break;
                    case "Charts":
                        newWindow = new JNR.Views.Charts();
                        break;
                    // No "Search" window directly, MainPage is the search hub
                    // "Settings" and "Links" are placeholders
                    case "Settings":
                    case "Links":
                        MessageBox.Show($"{viewName} page not yet implemented.", "Coming Soon");
                        // Re-check the "About" button as we are not navigating away
                        FindAboutRadioButtonAndCheck();
                        return;
                }

                if (newWindow != null)
                {
                    newWindow.Owner = Application.Current.MainWindow; // Or this.Owner if preferred
                    newWindow.Show();
                    this.Close();
                }
            }
        }

        private void GoToSearch_Click(object sender, RoutedEventArgs e)
        {
            var mainPage = new JNR.Views.MainPage.MainPage();
            mainPage.Show();
            this.Close();
        }

        private void FindAboutRadioButtonAndCheck()
        {
            // This method is to re-check the About button if navigation to another view fails (e.g. placeholder)
            // It assumes a similar structure to your other views for finding the RadioButton.
            // You might need to adjust if the sidebar XAML structure is different.
            var sidebar = (this.Content as FrameworkElement)?.FindName("SidebarContentPanel") as StackPanel; // Example name
            if (sidebar == null) // Try a more generic way if no name
            {
                var mainGrid = VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(this.Content as Border, 0) as Border, 0) as Viewbox, 0) as Grid;
                if (mainGrid != null && mainGrid.Children.Count > 1)
                {
                    // Assuming sidebar is the first StackPanel in the second row, first column element
                    sidebar = mainGrid.Children.OfType<StackPanel>().FirstOrDefault(p => Grid.GetRow(p) == 1 && Grid.GetColumn(p) == 0);
                }
            }

            if (sidebar != null)
            {
                foreach (var child in sidebar.Children)
                {
                    if (child is RadioButton rb && rb.Content?.ToString() == "About")
                    {
                        rb.IsChecked = true;
                        break;
                    }
                }
            }
        }
    }
}