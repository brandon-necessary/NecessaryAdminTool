using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NecessaryAdminTool.Managers.UI;

namespace NecessaryAdminTool
{
    // TAG: #VERSION_7 #CONNECTION_PROFILES #UI
    /// <summary>
    /// Connection Profile management dialog
    /// </summary>
    public partial class ConnectionProfileDialog : Window
    {
        private ObservableCollection<ConnectionProfile> _profiles;
        public ConnectionProfile SelectedProfile { get; private set; }

        public ConnectionProfileDialog()
        {
            InitializeComponent();
            LoadProfiles();
        }

        private void LoadProfiles()
        {
            _profiles = new ObservableCollection<ConnectionProfile>(ConnectionProfileManager.GetProfiles());
            GridProfiles.ItemsSource = _profiles;
        }

        private void GridProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = GridProfiles.SelectedItem != null;
            BtnEditProfile.IsEnabled = hasSelection;
            BtnDeleteProfile.IsEnabled = hasSelection;
            BtnLoadProfile.IsEnabled = hasSelection;
        }

        private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ConnectionProfileEditDialog(null);
            if (dialog.ShowDialog() == true)
            {
                ConnectionProfileManager.SaveProfile(dialog.Profile);
                LoadProfiles();
            }
        }

        private void BtnEditProfile_Click(object sender, RoutedEventArgs e)
        {
            if (GridProfiles.SelectedItem is ConnectionProfile profile)
            {
                var dialog = new ConnectionProfileEditDialog(profile);
                if (dialog.ShowDialog() == true)
                {
                    ConnectionProfileManager.SaveProfile(dialog.Profile);
                    LoadProfiles();
                }
            }
        }

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (GridProfiles.SelectedItem is ConnectionProfile profile)
            {
                ToastManager.ShowWarning($"Are you sure you want to delete the profile '{profile.Name}'? This cannot be undone.", "Delete", () =>
                {
                    ConnectionProfileManager.DeleteProfile(profile.Name);
                    LoadProfiles();
                });
            }
        }

        private void BtnLoadProfile_Click(object sender, RoutedEventArgs e)
        {
            if (GridProfiles.SelectedItem is ConnectionProfile profile)
            {
                SelectedProfile = profile;

                // Update last used date
                profile.LastUsedDate = DateTime.Now;
                ConnectionProfileManager.SaveProfile(profile);

                DialogResult = true;
                Close();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
