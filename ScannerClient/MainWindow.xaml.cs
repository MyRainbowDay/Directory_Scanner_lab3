using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.Win32;



namespace ScannerClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Size of our form variable
        private bool isMaximized = false;

        // Storing URIs
        private Uri folderUri, textFileUri, normalFileUri;

        public MainWindow()
        {
            string str = Environment.CurrentDirectory;
            InitializeComponent();

            // URIs for selecting icons
            folderUri = new Uri($@"{Environment.CurrentDirectory}\..\..\..\Images\folder.png");
            normalFileUri = new Uri($@"{Environment.CurrentDirectory}\..\..\..\Images\file.png");
            textFileUri = new Uri($@"{Environment.CurrentDirectory}\..\..\..\Images\textFile.png");
        }

        // Form scrin methods

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Click on the form 2 times

            if (e.ClickCount == 2)
            {
                // Set normal scrin size
                if (isMaximized)
                {
                    this.WindowState = WindowState.Normal;
                    this.Width = 1080;
                    this.Height = 720;

                    isMaximized = false;
                }
                // Set maximum scrin size
                else
                {
                    this.WindowState = WindowState.Maximized;

                    isMaximized = true;
                }
            }
        }

        // Send something like a signal to the MyScannerLibrary (Method will change flags in it)
        private void StopGauging_Btn_Click(object sender, RoutedEventArgs e)
        {
            MyScannerLibrary.DirScanner.StopProcessing();
        }

        // Method for resizing our form
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // Button for start gauging selected directory size
        private async void GaugeDir_Btn_Click(object sender, RoutedEventArgs e)
        {
            // Create openFileDialog and set filters for a directory
            OpenFileDialog folderBrowser = new OpenFileDialog();
            folderBrowser.ValidateNames = false;
            folderBrowser.CheckFileExists = false;
            folderBrowser.CheckPathExists = true;
            folderBrowser.FileName = "Folder Selection.";

            // If user selected directory successfully
            string? folderPath = string.Empty;
            if (folderBrowser.ShowDialog() == true)
            {
                // Get and save its name
                folderPath = Path.GetDirectoryName(folderBrowser.FileName);

                // if there is a mistake taking folder path -> throw an exception as a message box 
                if (folderPath == null)
                {
                    MessageBox.Show("Error. Folder path is null!");
                    return;
                }
            }

            // Start proceeding our directory asynchroniously. Return to the main vies without UI block
            var task = Task.Run(() => TaskForAnAsyncOperation(folderPath));
            var entities = await task;

            // When we are done -> return management to this function and draw a tree with the material our librart has taken
            TreeView treeView = GenerateTreeViewFromTheEntities(null, entities, 0, null);

            // If we processed directory before -> remove it to place the new one (replace it)
            if (DirectoryTreeView.Children.Count > 0)
                DirectoryTreeView.Children.RemoveAt(0);

            // Show to the user our new generated tree
            DirectoryTreeView.Children.Add(treeView);
        }

        // Method when user press logout button
        private void Logout_Btn_Click(object sender, RoutedEventArgs e)
        {
            //Exit the application
            Application.Current.Shutdown();
        }

        // Methods for usage (Not connected with xaml items)

        // Method wich generate treeView object from the entities we have received from the library
        private TreeView GenerateTreeViewFromTheEntities(TreeViewItem treeItem, List<DirectoryScanner.Entity> entities, int index, DirectoryInfo subDir)
        {
            TreeView treeView = null; // Make a variable wich will store link for a generated tree

            // Set method proceeding as a main to the Thred (needed to prevent access to this method data from another threads)
            Application.Current.Dispatcher.Invoke(() =>
            {
                treeView = new TreeView(); // generate tree in the hip
                TreeViewItem tempItem = new TreeViewItem(); // generate default treeView item for storing data in the while loop

                // Proceed through all list of entities
                while (index <= entities.Count - 1)
                {
                    // If we are working with head directory (subdir is null) or we are dealing with any file wich is inside previous directory
                    if (index == 0 || entities[index].SubDirecory.FullName == subDir?.FullName)
                    {
                        // Getting file extension and total size in persantage 
                        string extension = entities[index].Type == DirectoryScanner.EntityType.File ? "(file)" : entities[index].Type == DirectoryScanner.EntityType.Directory ? "(dir)" : "(txt)";
                        string persantage = entities[index].Persantage == String.Empty ? "" : $", {entities[index].Persantage}";

                        var newTreeItem = new TreeViewItem(); // current entity treeViewItem

                        // Create a stockPanel object to store information about the entity with an icon
                        StackPanel stackPanel = new StackPanel() { Orientation = Orientation.Horizontal };

                        // File information
                        TextBlock textBlock = new TextBlock() { Text = extension + $" {entities[index].Name} ({entities[index].Size} байт{persantage})" };

                        // Set an icon depending on entity type (file, dir or txt)
                        string path = entities[index].Type == DirectoryScanner.EntityType.File ? normalFileUri.ToString() : entities[index].Type == DirectoryScanner.EntityType.Directory ? folderUri.ToString() : textFileUri.ToString();
                        Uri uri = new Uri(path);
                        var image = new Image() { Source = new BitmapImage(uri) };

                        // Add this items to the stockPanel
                        stackPanel.Children.Add(image);
                        stackPanel.Children.Add(textBlock);

                        // Add all collected data about file to the treeViewItem header
                        newTreeItem.Header = stackPanel;

                        if (treeItem == null) // If we dealing with the head directory -> add treeViewItem to the main tree
                        {
                            treeView.Items.Add(newTreeItem);
                            treeItem = newTreeItem;
                        }
                        else // If we are dealing with not a head dir -> add treeViewItem as a children to the prev treeViewItem
                        {
                            treeItem.Items.Add(newTreeItem);
                        }

                        tempItem = newTreeItem; // Save current item link in the temp variable

                        index += 1; // Increment index to select new entity item

                        continue;
                    }
                    else // If it is not head directory and we dont know anything about current file parent and its not connected with prev item
                    {
                        // If we are working with files wich are inside of previous file directory  
                        if (subDir == null || entities[index].SubDirecory.FullName.Contains(entities[index - 1].SubDirecory.FullName))
                        {
                            treeItem = tempItem; // set current treeItem as a current folder
                            subDir = entities[index].SubDirecory; // Change subdir to the current file dir
                            continue;
                        }
                        else // If we are not -> it means that this file in the previous directory (not in the current)
                        {
                            subDir = subDir.Parent; // set subdir as a parent dir
                            treeItem = (TreeViewItem)treeItem.Parent; // set current TreeItem as a parent because we took a step behind 
                            continue;
                        }
                    }
                }

                return treeView;
            });

            // Processing an error if occured
            if (treeView == null)
                throw new Exception("Error. Tree was not generated");

            return treeView;
        }

        // Method for usage as an async wich will call dll method to get all directory entities
        private Task<List<DirectoryScanner.Entity>> TaskForAnAsyncOperation(string folderPath)
        {
            var entities = DirectoryScanner.DirScanner.Scan(folderPath);

            // Return CompletedTask with a specific List<Entity> value
            return Task.FromResult<List<DirectoryScanner.Entity>>(entities);
        }
    }
}
