// App.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls; // For RadioButton
using System.Windows.Media;    // For VisualTreeHelper
using JNR.Views;             // Namespace for About, Charts, etc. (if they are directly in JNR.Views)
using JNR.Views.MainPage;    // Namespace for MainPage
using JNR.Views.My_Albums;   // Namespace for MyAlbums
using JNR.Views.Genres;   // Namespace for LoginView
using JNR.Views.SignUp;      // Namespace for SignUp

namespace JNR // Ensure this is the root namespace of your project
{
    public partial class App : Application
    {
        private static Dictionary<Type, Window> openWindows = new Dictionary<Type, Window>();

        public static void NavigateTo<TView>(Window currentWindowToClose) where TView : Window, new()
        {
            Type viewType = typeof(TView);

            // Unregister and close the current window if it's not null
            if (currentWindowToClose != null)
            {
                WindowClosed(currentWindowToClose); // Unregister
                currentWindowToClose.Close();
            }

            Window windowToOpen;
            if (openWindows.TryGetValue(viewType, out Window existingWindow))
            {
                windowToOpen = existingWindow;
                if (windowToOpen.WindowState == WindowState.Minimized)
                {
                    windowToOpen.WindowState = WindowState.Normal;
                }
                windowToOpen.Activate();
                // No need to re-register or re-hook event if already managed
            }
            else
            {
                windowToOpen = new TView();
                openWindows[viewType] = windowToOpen; // Register new window
                windowToOpen.Closed += (s, args) => WindowClosed(windowToOpen); // Hook closed event
                windowToOpen.Show();
            }
        }

        public static void NavigateToMainPage(Window currentWindowToClose)
        {
            NavigateTo<JNR.Views.MainPage.MainPage>(currentWindowToClose);
        }

        public static void NavigateToOverview(Window currentWindowToClose, string albumName, string artistName, string mbid, string coverArtUrl)
        {
            Type overviewType = typeof(JNR.Views.Overview); // Assuming Overview is in JNR.Views

            if (currentWindowToClose != null)
            {
                WindowClosed(currentWindowToClose);
                currentWindowToClose.Close();
            }

            // Always create a new Overview. If an old one exists in tracking, close it.
            if (openWindows.TryGetValue(overviewType, out Window existingOverviewWindow))
            {
                WindowClosed(existingOverviewWindow); // Unregister
                existingOverviewWindow.Close();
            }

            JNR.Views.Overview overviewWindow = new JNR.Views.Overview(albumName, artistName, mbid, coverArtUrl);
            openWindows[overviewType] = overviewWindow; // Register the new one
            overviewWindow.Closed += (s, args) => WindowClosed(overviewWindow); // Hook closed event
            overviewWindow.Show();
        }

        // ========= NEW METHOD FOR PROFILE NAVIGATION =========
        public static void NavigateToProfile(Window currentWindowToClose, int userId)
        {
            Type profileType = typeof(JNR.Views.Profile);

            if (currentWindowToClose != null)
            {
                WindowClosed(currentWindowToClose);
                currentWindowToClose.Close();
            }

            // To prevent multiple instances of the *same* profile window type from causing issues,
            // we'll treat it like Overview and always create a new one.
            if (openWindows.TryGetValue(profileType, out Window existingProfileWindow))
            {
                WindowClosed(existingProfileWindow);
                existingProfileWindow.Close();
            }

            JNR.Views.Profile profileWindow = new JNR.Views.Profile(userId);
            openWindows[profileType] = profileWindow; // Register the new one
            profileWindow.Closed += (s, args) => WindowClosed(profileWindow); // Hook closed event
            profileWindow.Show();
        }
        // ========= END NEW METHOD =========

        public static void HandlePlaceholderNavigation(Window currentWindow, RadioButton placeholderRadioButton, string placeholderContent)
        {
            MessageBox.Show($"{placeholderContent} page not yet implemented.", "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);

            if (placeholderRadioButton != null)
            {
                placeholderRadioButton.IsChecked = false; // Uncheck the clicked placeholder
            }

            // Re-check the RadioButton corresponding to the current window type.
            // Each view that can be a 'currentWindow' here needs 'EnsureCorrectRadioButtonIsChecked'.
            if (currentWindow is JNR.Views.About aboutWin) aboutWin.EnsureCorrectRadioButtonIsChecked();
            else if (currentWindow is JNR.Views.Charts chartsWin) chartsWin.EnsureCorrectRadioButtonIsChecked();
            else if (currentWindow is JNR.Views.Genres.Genres genresWin) genresWin.EnsureCorrectRadioButtonIsChecked();
            else if (currentWindow is JNR.Views.My_Albums.MyAlbums myAlbumsWin) myAlbumsWin.EnsureCorrectRadioButtonIsChecked();
            // Overview and Profile typically don't have a self-representing RadioButton in a shared sidebar.
        }

        public static void WindowClosed(Window closedWindow)
        {
            if (closedWindow != null && openWindows.ContainsKey(closedWindow.GetType()))
            {
                openWindows.Remove(closedWindow.GetType());
            }

            if (openWindows.Count == 0 && Application.Current.Windows.Count == 0)
            {
                //This condition might be hit if the very last window closes.
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // this.ShutdownMode = ShutdownMode.OnLastWindowClose;
        }
    }
}