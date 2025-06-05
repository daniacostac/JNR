// File: ViewModels/Login/LoginView.xaml.cs (or Views/Login/LoginView.xaml.cs)
using JNR.Views.SignUp;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using JNR.Models;
using JNR.Helpers; // For PasswordHasher AND SessionManager
using Microsoft.EntityFrameworkCore;

namespace JNR.Views // Or JNR.Views.Login
{
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = txbUsername.Text;
            string password = txbPassword.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter both username and password.", "Login Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                try
                {
                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);

                    if (user == null)
                    {
                        MessageBox.Show("Invalid username or password.", "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (PasswordHasher.VerifyPassword(password, user.PasswordHash))
                    {
                        // Set current user in SessionManager
                        SessionManager.CurrentUserId = user.UserId;
                        SessionManager.CurrentUsername = user.Username;

                        MessageBox.Show($"Login successful! Welcome {user.Username}.", "Login Success", MessageBoxButton.OK, MessageBoxImage.Information);

                        JNR.Views.MainPage.MainPage mainPage = new JNR.Views.MainPage.MainPage();
                        mainPage.Show();
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Invalid username or password.", "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred during login: {ex.Message}", "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Console.WriteLine($"LOGIN EXCEPTION: {ex.ToString()}");
                }
            }
        }

        private void SignUp_Click(object sender, MouseButtonEventArgs e)
        {
            JNR.Views.SignUp.SignUp signUpWindow = new JNR.Views.SignUp.SignUp();
            signUpWindow.Show();
            this.Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}