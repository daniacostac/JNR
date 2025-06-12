// File: Views/Settings/Settings.xaml.cs

// ========= NEW/REQUIRED USING STATEMENTS =========
using Microsoft.Win32; // For OpenFileDialog
using System.IO;       // For Path, File, and Directory operations
using System.Windows.Media.Imaging; // For BitmapImage and ImageSource
// ===============================================

using JNR.Helpers;
using JNR.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JNR.Views.Settings
{
    public partial class Settings : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region Properties for Data Binding
        private string _username;
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }

        private string _email;
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }

        private bool _isEditingProfile;
        public bool IsEditingProfile
        {
            get => _isEditingProfile;
            set { _isEditingProfile = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotEditingProfile)); }
        }
        public bool IsNotEditingProfile => !IsEditingProfile;

        private string _editedUsername;
        public string EditedUsername { get => _editedUsername; set { _editedUsername = value; OnPropertyChanged(); } }

        private string _editedEmail;
        public string EditedEmail { get => _editedEmail; set { _editedEmail = value; OnPropertyChanged(); } }

        private string _profileStatusMessage;
        public string ProfileStatusMessage { get => _profileStatusMessage; set { _profileStatusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasProfileStatusMessage)); } }

        public bool HasProfileStatusMessage => !string.IsNullOrEmpty(_profileStatusMessage);

        private Brush _profileStatusBrush;
        public Brush ProfileStatusBrush { get => _profileStatusBrush; set { _profileStatusBrush = value; OnPropertyChanged(); } }

        // ========= NEW PROPERTY FOR PROFILE PICTURE BINDING =========
        private ImageSource _profilePictureSource;
        public ImageSource ProfilePictureSource
        {
            get => _profilePictureSource;
            set { _profilePictureSource = value; OnPropertyChanged(); }
        }
        // ============================================================

        #endregion

        public Settings()
        {
            InitializeComponent();
            this.DataContext = this;
            this.Loaded += async (s, e) => await LoadUserSettingsAsync();
            this.Closed += (s, args) => App.WindowClosed(this);
            ProfileStatusBrush = Brushes.LightGreen; // Default to success color
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

                    // ========= MODIFIED: LOAD PROFILE PICTURE =========
                    if (!string.IsNullOrEmpty(user.ProfilePicturePath) && File.Exists(user.ProfilePicturePath))
                    {
                        // Use a BitmapImage with OnLoad cache option to prevent locking the file.
                        // This reads the entire image into memory and closes the file stream.
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(user.ProfilePicturePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        ProfilePictureSource = bitmap;
                    }
                    else
                    {
                        // Load a default image from application resources if no picture is set or the file is missing.
                        ProfilePictureSource = new BitmapImage(new Uri("pack://application:,,,/Images/user-icon.png"));
                    }
                    // ================================================
                }
            }
        }

        #region Profile Editing Logic

        private void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            EditedUsername = Username;
            EditedEmail = Email;
            ProfileStatusMessage = string.Empty;
            IsEditingProfile = true;
        }

        private void CancelEditProfile_Click(object sender, RoutedEventArgs e)
        {
            IsEditingProfile = false;
            ProfileStatusMessage = string.Empty;
        }

        private async void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EditedUsername) || string.IsNullOrWhiteSpace(EditedEmail))
            {
                SetProfileStatus("Username and Email cannot be empty.", isError: true);
                return;
            }
            if (!IsValidEmail(EditedEmail))
            {
                SetProfileStatus("Please enter a valid email address.", isError: true);
                return;
            }

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                var currentUserId = SessionManager.CurrentUserId.Value;

                if (EditedUsername != Username && await dbContext.Users.AnyAsync(u => u.Username == EditedUsername && u.UserId != currentUserId))
                {
                    SetProfileStatus("That username is already taken. Please choose another.", isError: true);
                    return;
                }
                if (EditedEmail != Email && await dbContext.Users.AnyAsync(u => u.Email == EditedEmail && u.UserId != currentUserId))
                {
                    SetProfileStatus("That email is already registered to another account.", isError: true);
                    return;
                }

                var user = await dbContext.Users.FindAsync(currentUserId);
                if (user != null)
                {
                    user.Username = EditedUsername;
                    user.Email = EditedEmail;
                    await dbContext.SaveChangesAsync();

                    Username = user.Username;
                    Email = user.Email;
                    SessionManager.CurrentUsername = user.Username;

                    IsEditingProfile = false;
                    SetProfileStatus("Profile updated successfully!", isError: false);
                }
                else
                {
                    SetProfileStatus("Could not find user profile to update.", isError: true);
                }
            }
        }

        // ========= NEW METHOD: CHANGE PROFILE PICTURE =========
        private async void ChangePicture_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select a Profile Picture",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string selectedFilePath = openFileDialog.FileName;

                    // Define the central storage location for profile pictures
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string profilePicsDir = Path.Combine(baseDir, "UserData", "ProfilePictures");

                    // Ensure the directory exists (it should from App.xaml.cs, but this is a safeguard)
                    Directory.CreateDirectory(profilePicsDir);

                    // Create a unique filename based on the user's ID to prevent conflicts
                    string extension = Path.GetExtension(selectedFilePath);
                    string newFileName = $"user_{SessionManager.CurrentUserId.Value}{extension}";
                    string newFilePath = Path.Combine(profilePicsDir, newFileName);

                    // Copy the user's selected file to our application's managed folder.
                    // The 'true' argument allows overwriting the file if it already exists.
                    File.Copy(selectedFilePath, newFilePath, true);

                    // Save the path to the *newly copied file* in the database
                    var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
                    string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
                    optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                    using (var dbContext = new JnrContext(optionsBuilder.Options))
                    {
                        var user = await dbContext.Users.FindAsync(SessionManager.CurrentUserId.Value);
                        if (user != null)
                        {
                            user.ProfilePicturePath = newFilePath;
                            await dbContext.SaveChangesAsync();
                        }
                    }

                    // Update the UI immediately by loading the new image into the ImageSource property
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(newFilePath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // Crucial to avoid file locking
                    bitmap.EndInit();
                    ProfilePictureSource = bitmap;

                    MessageBox.Show("Profile picture updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while updating your picture: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        // ======================================================

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                email = Regex.Replace(email, @"(@)(.+)$", DomainMapper, RegexOptions.None, TimeSpan.FromMilliseconds(200));
                string DomainMapper(Match match)
                {
                    var idn = new System.Globalization.IdnMapping();
                    return match.Groups[1].Value + idn.GetAscii(match.Groups[2].Value);
                }
            }
            catch { return false; }

            try
            {
                return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch { return false; }
        }

        private void SetProfileStatus(string message, bool isError)
        {
            ProfileStatusMessage = message;
            ProfileStatusBrush = isError ? new SolidColorBrush(Color.FromRgb(255, 107, 107)) : new SolidColorBrush(Color.FromRgb(107, 255, 135));
        }

        #endregion

        #region Existing Logic (Unchanged)
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
            SessionManager.CurrentUserId = null;
            SessionManager.CurrentUsername = null;
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
                        dbContext.Users.Remove(user);
                        await dbContext.SaveChangesAsync();

                        MessageBox.Show("Your account has been successfully deleted.", "Account Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                        Logout_Click(null, null);
                    }
                }
            }
        }

        public void EnsureCorrectRadioButtonIsChecked()
        {
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
                        App.HandlePlaceholderNavigation(this, rb, viewName);
                        break;
                }
            }
        }

        private void btnGoBack_Click(object sender, RoutedEventArgs e) => App.NavigateToMainPage(this);
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        #endregion
    }
}