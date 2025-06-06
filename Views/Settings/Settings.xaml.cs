// File: Views/Settings/Settings.xaml.cs
using JNR.Helpers;
using JNR.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JNR.Views.Settings
{
    public partial class Settings : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _username;
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        private string _email;
        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public Settings()
        {
            InitializeComponent();
            this.DataContext = this;
            this.Loaded += async (s, e) => await LoadUserSettingsAsync();
            this.Closed += (s, args) => App.WindowClosed(this);
        }

        private async Task LoadUserSettingsAsync()
        {
            if (!SessionManager.CurrentUserId.HasValue)
            {
                MessageBox.Show("No user is currently logged in. Please log in to view settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                App.NavigateTo<LoginView>(this); // Redirect to login
                return;
            }

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                var user = await dbContext.Users.FindAsync(SessionManager.CurrentUserId.Value);
                if (user != null)
                {
                    Username = user.Username;
                    Email = user.Email;
                }
            }
        }

        private async void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            string currentPassword = pbCurrentPassword.Password;
            string newPassword = pbNewPassword.Password;
            string confirmPassword = pbConfirmPassword.Password;

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                MessageBox.Show("All password fields are required.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPassword.Length < 6)
            {
                MessageBox.Show("The new password must be at least 6 characters long.", "Weak Password", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPassword != confirmPassword)
            {
                MessageBox.Show("The new passwords do not match.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                var user = await dbContext.Users.FindAsync(SessionManager.CurrentUserId.Value);
                if (user == null)
                {
                    MessageBox.Show("Could not find the current user.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!PasswordHasher.VerifyPassword(currentPassword, user.PasswordHash))
                {
                    MessageBox.Show("The current password you entered is incorrect.", "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                user.PasswordHash = PasswordHasher.HashPassword(newPassword);
                await dbContext.SaveChangesAsync();

                MessageBox.Show("Your password has been changed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                pbCurrentPassword.Clear();
                pbNewPassword.Clear();
                pbConfirmPassword.Clear();
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            // Clear session
            SessionManager.CurrentUserId = null;
            SessionManager.CurrentUsername = null;

            // Navigate to login screen
            App.NavigateTo<LoginView>(this);
        }

        private async void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            string confirmationText = $"Are you absolutely sure you want to delete your account '{Username}'? This will permanently erase all your ratings, reviews, and collection data. This action cannot be undone.";
            MessageBoxResult result = MessageBox.Show(confirmationText, "Confirm Account Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (!SessionManager.CurrentUserId.HasValue) return;

                var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
                string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
                optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

                using (var dbContext = new JnrContext(optionsBuilder.Options))
                {
                    var user = await dbContext.Users
                        .Include(u => u.Useralbumratings)
                        .FirstOrDefaultAsync(u => u.UserId == SessionManager.CurrentUserId.Value);

                    if (user != null)
                    {
                        // EF Core will handle deleting dependent entities (Useralbumratings)
                        dbContext.Users.Remove(user);
                        await dbContext.SaveChangesAsync();

                        MessageBox.Show("Your account has been successfully deleted.", "Account Deleted", MessageBoxButton.OK, MessageBoxImage.Information);

                        // Log out and navigate to login
                        Logout_Click(null, null);
                    }
                }
            }
        }

        // --- Standard Window and Navigation Logic ---

        public void EnsureCorrectRadioButtonIsChecked()
        {
            // This is used by the App's placeholder navigation handler to re-select this view's button
            var settingsRadioButton = SidebarContentPanel.Children.OfType<RadioButton>()
                .FirstOrDefault(r => r.Content?.ToString() == "Settings");
            if (settingsRadioButton != null)
            {
                settingsRadioButton.IsChecked = true;
            }
        }

        private void SidebarNavigation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.CommandParameter is string viewName)
            {
                switch (viewName)
                {
                    case "MyAlbums": App.NavigateTo<My_Albums.MyAlbums>(this); break;
                    case "Genres": App.NavigateTo<Genres.Genres>(this); break;
                    case "Charts": App.NavigateTo<Charts>(this); break;
                    case "About": App.NavigateTo<About>(this); break;
                    case "Links":
                        // Use the central placeholder handler
                        App.HandlePlaceholderNavigation(this, rb, viewName);
                        break;
                }
            }
        }

        private void btnGoBack_Click(object sender, RoutedEventArgs e) => App.NavigateToMainPage(this);
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
    }
}