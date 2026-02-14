using System.Windows;
using System.Windows.Controls;

namespace ArtaznIT
{
    // TAG: #VERSION_7 #BOOKMARKS #UI
    /// <summary>
    /// Dialog for adding/editing bookmark details
    /// </summary>
    public partial class BookmarkEditDialog : Window
    {
        public string Category { get; private set; }
        public string Description { get; private set; }

        public BookmarkEditDialog(string hostname)
        {
            InitializeComponent();
            TxtHostname.Text = $"Computer: {hostname}";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Get category (from selected item or typed text if editable)
            if (ComboCategory.SelectedItem is ComboBoxItem selectedItem)
            {
                Category = selectedItem.Content.ToString();
            }
            else if (!string.IsNullOrWhiteSpace(ComboCategory.Text))
            {
                Category = ComboCategory.Text.Trim();
            }
            else
            {
                Category = "General";
            }

            Description = TxtDescription.Text.Trim();

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
