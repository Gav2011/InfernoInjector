using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace InfernoInjector
{
    public partial class ProcessSelectionWindow : Window
    {
        public string SelectedProcessName { get; private set; }
        public int SelectedProcessId { get; private set; }

        private List<ProcessInfo> _allProcesses;

        public ProcessSelectionWindow()
        {
            InitializeComponent();
            LoadProcesses();
        }

        // Load all processes into the ListView
        private void LoadProcesses()
        {
            _allProcesses = Process.GetProcesses()
                .Where(p => !string.IsNullOrEmpty(p.ProcessName)) // Filter out processes without names
                .Select(p => new ProcessInfo { ProcessName = p.ProcessName, Id = p.Id })
                .OrderBy(p => p.ProcessName)
                .ToList();

            ProcessesListView.ItemsSource = _allProcesses;
        }

        // Handle process name filtering
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filterText = SearchTextBox.Text.ToLower();
            var filteredProcesses = _allProcesses.Where(p => p.ProcessName.ToLower().Contains(filterText)).ToList();
            ProcessesListView.ItemsSource = filteredProcesses;
        }

        // Handle selecting a process (double-click)
        private void ProcessesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ProcessesListView.SelectedItem != null)
            {
                var selectedProcess = (ProcessInfo)ProcessesListView.SelectedItem;
                SelectedProcessName = selectedProcess.ProcessName;
                SelectedProcessId = selectedProcess.Id;
                DialogResult = true;  // Close the dialog and return the selected process info
            }
        }

        // Handle GotFocus event for the SearchTextBox
        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Clear placeholder text when the user focuses the search box
            if (SearchTextBox.Text == "Search by name...")
            {
                SearchTextBox.Text = string.Empty;
                SearchTextBox.Foreground = new SolidColorBrush(Colors.White); // Change text color to white
            }
        }

        // Handle LostFocus event for the SearchTextBox
        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Restore placeholder text if the user leaves the search box empty
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
            {
                SearchTextBox.Text = "Search by name...";
                SearchTextBox.Foreground = new SolidColorBrush(Colors.Gray); // Change text color to gray
            }
        }

        // Process info class to bind process details
        public class ProcessInfo
        {
            public string ProcessName { get; set; }
            public int Id { get; set; }
        }
    }
}
