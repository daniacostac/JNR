// File: ViewModels\SignUp\SignUp.xaml.cs
// (Adjust namespace if it's directly in Views)
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using JNR.Models; // For JnrDbContext and User entity
using JNR.Helpers; // For PasswordHasher
using Microsoft.EntityFrameworkCore; // For EF Core operations

namespace JNR.Views.SignUp
{
    public partial class SignUp : Window
    {
        public SignUp()
        {
            InitializeComponent();
        }

        private async void SignUpButton_Click(object sender, RoutedEventArgs e) // Assuming your button is named SignUpButton
        {
            string username = txbUsernameReg.Text;
            string email = txbEmail.Text; // Assuming you have a txbEmail TextBox
            string password = txbPasswordReg.Password;

            // Basic Validations
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("All fields are required.", "Registration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsValidEmail(email))
            {
                MessageBox.Show("Please enter a valid email address.", "Registration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Add password complexity rules if desired (e.g., length, characters)

            // DbContext Configuration (same as LoginView)
            var optionsBuilder = new DbContextOptionsBuilder<JnrContext>();
            string connectionString = "Server=localhost;Port=3306;Database=jnr;Uid=root;Pwd=root;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

            using (var dbContext = new JnrContext(optionsBuilder.Options))
            {
                try
                {
                    // Check if username already exists
                    bool usernameExists = await dbContext.Users.AnyAsync(u => u.Username == username);
                    if (usernameExists)
                    {
                        MessageBox.Show("Username already taken. Please choose another one.", "Registration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Check if email already exists
                    bool emailExists = await dbContext.Users.AnyAsync(u => u.Email == email);
                    if (emailExists)
                    {
                        MessageBox.Show("Email address already registered. Please use a different email or login.", "Registration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Hash the password
                    string hashedPassword = PasswordHasher.HashPassword(password);

                    // Create new user
                    User newUser = new User
                    {
                        Username = username,
                        Email = email,
                        PasswordHash = hashedPassword,
                        CreatedAt = DateTime.UtcNow // Use UtcNow for consistency
                    };

                    dbContext.Users.Add(newUser);
                    await dbContext.SaveChangesAsync();

                    MessageBox.Show("Registration successful! You can now log in.", "Registration Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Navigate to LoginView
                    LoginView loginView = new LoginView();
                    loginView.Show();
                    this.Close();

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred during registration: {ex.Message}", "Registration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    // Log the full exception details for debugging
                    Console.WriteLine($"SIGNUP EXCEPTION: {ex.ToString()}");
                }
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // Optional: Add a button/link to go back to Login from SignUp
        private void GoToLogin_Click(object sender, RoutedEventArgs e) // Or MouseButtonEventArgs
        {
            LoginView loginView = new LoginView();
            loginView.Show();
            this.Close();
        }
    }
}