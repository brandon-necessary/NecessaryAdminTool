using System;
using System.Windows;
using System.Windows.Controls;
using NecessaryAdminTool.Managers.UI;

namespace NecessaryAdminTool
{
    // TAG: #VERSION_7 #CONNECTION_PROFILES #UI
    /// <summary>
    /// Edit dialog for connection profiles
    /// </summary>
    public partial class ConnectionProfileEditDialog : Window
    {
        public ConnectionProfile Profile { get; private set; }
        private bool _isEditMode;

        public ConnectionProfileEditDialog(ConnectionProfile existingProfile)
        {
            InitializeComponent();

            if (existingProfile != null)
            {
                // Edit mode
                _isEditMode = true;
                TxtHeader.Text = "✏️ EDIT CONNECTION PROFILE";
                Profile = existingProfile;

                // Populate fields
                TxtProfileName.Text = existingProfile.Name;
                TxtDomainController.Text = existingProfile.DomainController;
                TxtUsername.Text = existingProfile.Username;
                TxtDomain.Text = existingProfile.Domain;
                TxtDescription.Text = existingProfile.Description;

                // Set environment combo
                foreach (ComboBoxItem item in ComboEnvironment.Items)
                {
                    if (item.Content.ToString().Equals(existingProfile.Environment, StringComparison.OrdinalIgnoreCase))
                    {
                        ComboEnvironment.SelectedItem = item;
                        break;
                    }
                }
            }
            else
            {
                // New mode
                _isEditMode = false;
                TxtHeader.Text = "➕ NEW CONNECTION PROFILE";
                Profile = new ConnectionProfile();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(TxtProfileName.Text))
            {
                ToastManager.ShowWarning("Profile Name is required.");
                TxtProfileName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtDomainController.Text))
            {
                ToastManager.ShowWarning("Domain Controller is required.");
                TxtDomainController.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtUsername.Text))
            {
                ToastManager.ShowWarning("Username is required.");
                TxtUsername.Focus();
                return;
            }

            // Check for duplicate profile name (only in new mode or if name changed)
            string newName = TxtProfileName.Text.Trim();
            if (!_isEditMode || !newName.Equals(Profile.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (ConnectionProfileManager.ProfileExists(newName))
                {
                    ToastManager.ShowWarning($"A profile named '{newName}' already exists. Please choose a different name.");
                    TxtProfileName.Focus();
                    return;
                }
            }

            // Save profile
            Profile.Name = newName;
            Profile.DomainController = TxtDomainController.Text.Trim();
            Profile.Username = TxtUsername.Text.Trim();
            Profile.Domain = TxtDomain.Text.Trim();
            Profile.Description = TxtDescription.Text.Trim();
            Profile.Environment = (ComboEnvironment.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Production";

            if (!_isEditMode)
            {
                Profile.CreatedDate = DateTime.Now;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
