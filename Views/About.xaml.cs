using System;
using System.Diagnostics;
using System.Reflection; // For Assembly version
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media; // Required for VisualTreeHelper
using System.Windows.Navigation; // For RequestNavigateEventArgs
using System.Linq; // Required for OfType()
using JNR; // <--- Add this for App class access

// Add using statements for other specific views if needed for App.NavigateTo type parameters
// using JNR.Views.My_Albums;
// using JNR.Views.Genres;
// using JNR.Views.Charts;

namespace JNR.Views
{
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
            this.Closed += (s, args) => App.WindowClosed(this); // Step 1: Register for central tracking

            LoadVersionInfo();
            // Update copyright year dynamically - ensure your alias is correct
            CopyrightTextBlock.Text = $"© {DateTime.Now.Year} Daniel Acosta Castilla. All rights reserved.";
        }

        // Step 2: Implement EnsureCorrectRadioButtonIsChecked
        public void EnsureCorrectRadioButtonIsChecked()
        {
            // Assuming the sidebar StackPanel in About.xaml is named "SidebarContentPanel"
            // or can be found by its structure.
            var sidebarPanel = FindVisualChild<StackPanel>(this, "SidebarContentPanel");
            if (sidebarPanel == null) // Fallback if not named, based on common structure
            {
                var mainGrid = VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(this.Content as Border, 0) as Border, 0) as Viewbox, 0) as Grid;
                if (mainGrid != null)
                {
                    sidebarPanel = mainGrid.Children.OfType<StackPanel>().FirstOrDefault(p => Grid.GetRow(p) == 1 && Grid.GetColumn(p) == 0);
                }
            }

            if (sidebarPanel != null)
            {
                foreach (var child in sidebarPanel.Children.OfType<RadioButton>())
                {
                    if (child.Content?.ToString() == "About")
                    {
                        child.IsChecked = true;
                        break;
                    }
                }
            }
            else
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Sidebar panel not found for re-checking About button in About view.");
            }
        }

        // Helper method (can be moved to a shared utility class later)
        // Copied here for completeness, ensure it's defined or accessible.
        public static T FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;
            T foundChild = null;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                T childType = child as T;
                if (childType == null)
                {
                    foundChild = FindVisualChild<T>(child, childName); // Recurse
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        foundChild = (T)child;
                        break;
                    }
                    else
                    {
                        foundChild = FindVisualChild<T>(child, childName);
                        if (foundChild != null) break;
                    }
                }
                else
                {
                    foundChild = (T)child;
                    break;
                }
            }
            return foundChild;
        }


        private void LoadVersionInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                VersionTextBlock.Text = version != null ? $"Version {version.Major}.{version.Minor}.{version.Build}" : "Version N/A";
            }
            catch (Exception ex)
            {
                VersionTextBlock.Text = "Version Error";
                Debug.WriteLine($"Error loading version info: {ex.Message}");
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

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

        // Step 3: Update SidebarNavigation_Click
        private void SidebarNavigation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.CommandParameter is string viewName)
            {
                if (viewName == "About")
                {
                    rb.IsChecked = true; // Ensure it's checked
                    return; // Already in this view
                }

                switch (viewName)
                {
                    case "MyAlbums": App.NavigateTo<JNR.Views.My_Albums.MyAlbums>(this); break;
                    case "Genres": App.NavigateTo<JNR.Views.Genres.Genres>(this); break;
                    case "Charts": App.NavigateTo<JNR.Views.Charts>(this); break;
                    case "Settings": App.NavigateTo<JNR.Views.Settings.Settings>(this); break;
                    // No "Search" window directly from About's sidebar usually.
                    case "Links":
                        App.HandlePlaceholderNavigation(this, rb, viewName);
                        return;
                }
            }
        }

        // Step 4: Update GoToSearch_Click
        private void GoToSearch_Click(object sender, RoutedEventArgs e)
        {
            App.NavigateToMainPage(this);
        }

        // Step 5: Remove FindAboutRadioButtonAndCheck (functionality moved to EnsureCorrectRadioButtonIsChecked)
        // private void FindAboutRadioButtonAndCheck() { /* ... old code ... */ }
    }
}