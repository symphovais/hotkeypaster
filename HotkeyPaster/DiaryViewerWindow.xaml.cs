using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TalkKeys.Services.Diary;

namespace TalkKeys
{
    public partial class DiaryViewerWindow : Window
    {
        private readonly IDiaryService _diaryService;
        private List<DateEntry> _allDates = new List<DateEntry>();
        private List<DiaryEntry> _currentEntries = new List<DiaryEntry>();
        private string _currentSearchQuery = string.Empty;

        public DiaryViewerWindow(IDiaryService diaryService)
        {
            _diaryService = diaryService ?? throw new ArgumentNullException(nameof(diaryService));
            InitializeComponent();
            Loaded += DiaryViewerWindow_Loaded;
        }

        private async void DiaryViewerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDatesAsync();
        }

        private async Task LoadDatesAsync()
        {
            try
            {
                var dates = await _diaryService.GetDatesWithEntriesAsync();
                _allDates = new List<DateEntry>();

                foreach (var date in dates.OrderByDescending(d => d))
                {
                    var entries = await _diaryService.GetEntriesAsync(date);
                    var entryCount = entries.Count();

                    _allDates.Add(new DateEntry
                    {
                        Date = date,
                        DisplayDate = FormatDate(date),
                        EntryCountText = $"{entryCount} {(entryCount == 1 ? "entry" : "entries")}",
                        EntryCount = entryCount
                    });
                }

                DateListBox.ItemsSource = _allDates;

                // Update total entries count
                int totalEntries = _allDates.Sum(d => d.EntryCount);
                TotalEntriesText.Text = $"{totalEntries} total {(totalEntries == 1 ? "entry" : "entries")}";

                // Select today if available, otherwise the most recent date
                var todayEntry = _allDates.FirstOrDefault(d => d.Date.Date == DateTime.Today);
                if (todayEntry != null)
                {
                    DateListBox.SelectedItem = todayEntry;
                }
                else if (_allDates.Any())
                {
                    DateListBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading diary dates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FormatDate(DateTime date)
        {
            var today = DateTime.Today;
            if (date.Date == today)
                return "Today";
            else if (date.Date == today.AddDays(-1))
                return "Yesterday";
            else if (date.Date >= today.AddDays(-7))
                return date.ToString("dddd"); // Day name for last week
            else
                return date.ToString("MMM dd, yyyy");
        }

        private async void DateListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateListBox.SelectedItem is DateEntry dateEntry)
            {
                await LoadEntriesForDateAsync(dateEntry.Date);
            }
        }

        private async Task LoadEntriesForDateAsync(DateTime date)
        {
            try
            {
                IEnumerable<DiaryEntry> entries;

                // If there's a search query, use search; otherwise get all entries for the date
                if (!string.IsNullOrWhiteSpace(_currentSearchQuery))
                {
                    entries = await _diaryService.SearchEntriesAsync(_currentSearchQuery, date, date);
                }
                else
                {
                    entries = await _diaryService.GetEntriesAsync(date);
                }

                _currentEntries = entries.OrderByDescending(e => e.Timestamp).ToList();

                // Update header
                SelectedDateText.Text = date.ToString("MMMM dd, yyyy");
                EntryCountText.Text = $"{_currentEntries.Count} {(_currentEntries.Count == 1 ? "entry" : "entries")}";

                // Clear and populate entries
                EntriesPanel.Children.Clear();

                if (_currentEntries.Any())
                {
                    EmptyState.Visibility = Visibility.Collapsed;

                    foreach (var entry in _currentEntries)
                    {
                        var entryCard = CreateEntryCard(entry);
                        EntriesPanel.Children.Add(entryCard);
                    }
                }
                else
                {
                    EmptyState.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading entries: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Border CreateEntryCard(DiaryEntry entry)
        {
            var border = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("BackgroundSecondary"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 16)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header with timestamp and metadata
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var timeBlock = new TextBlock
            {
                Text = entry.Timestamp.ToString("HH:mm:ss"),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = (System.Windows.Media.Brush)FindResource("AccentPrimary"),
                Margin = new Thickness(0, 0, 16, 0)
            };

            var metaPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var wordCountBlock = new TextBlock
            {
                Text = $"{entry.WordCount} words",
                FontSize = 12,
                Foreground = (System.Windows.Media.Brush)FindResource("TextMuted"),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            metaPanel.Children.Add(wordCountBlock);

            if (!string.IsNullOrEmpty(entry.Language))
            {
                var langBlock = new TextBlock
                {
                    Text = $"• {entry.Language}",
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextMuted"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                metaPanel.Children.Add(langBlock);
            }

            headerPanel.Children.Add(timeBlock);
            headerPanel.Children.Add(metaPanel);

            Grid.SetRow(headerPanel, 0);
            grid.Children.Add(headerPanel);

            // Entry text
            var textBlock = new TextBlock
            {
                Text = entry.Text,
                FontSize = 14,
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimary"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                Margin = new Thickness(0, 12, 0, 0)
            };

            Grid.SetRow(textBlock, 1);
            grid.Children.Add(textBlock);

            border.Child = grid;
            return border;
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentSearchQuery = SearchBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_currentSearchQuery))
            {
                // No search query - show all dates and reload current selection
                DateListBox.ItemsSource = _allDates;

                if (DateListBox.SelectedItem is DateEntry dateEntry)
                {
                    await LoadEntriesForDateAsync(dateEntry.Date);
                }
            }
            else
            {
                // Search across all entries
                await PerformSearchAsync(_currentSearchQuery);
            }
        }

        private async Task PerformSearchAsync(string query)
        {
            try
            {
                // Search all entries
                var results = await _diaryService.SearchEntriesAsync(query);
                var resultsByDate = results.GroupBy(e => e.Date)
                                          .OrderByDescending(g => g.Key)
                                          .ToList();

                // Filter date list to only dates with matching results
                var matchingDates = resultsByDate.Select(g => new DateEntry
                {
                    Date = g.Key,
                    DisplayDate = FormatDate(g.Key),
                    EntryCountText = $"{g.Count()} {(g.Count() == 1 ? "match" : "matches")}",
                    EntryCount = g.Count()
                }).ToList();

                DateListBox.ItemsSource = matchingDates;

                // Update total count
                int totalMatches = matchingDates.Sum(d => d.EntryCount);
                TotalEntriesText.Text = $"{totalMatches} {(totalMatches == 1 ? "match" : "matches")}";

                // Select first matching date
                if (matchingDates.Any())
                {
                    DateListBox.SelectedIndex = 0;
                }
                else
                {
                    // No matches
                    EntriesPanel.Children.Clear();
                    EmptyState.Visibility = Visibility.Visible;
                    SelectedDateText.Text = "No matches found";
                    EntryCountText.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching entries: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var diaryPath = _diaryService.GetDiaryDirectory();
                Process.Start("explorer.exe", diaryPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NewEntryButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Press Ctrl+Shift+D to record a new diary entry",
                "New Diary Entry",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // Helper class for date list items
        private class DateEntry
        {
            public DateTime Date { get; set; }
            public string DisplayDate { get; set; } = string.Empty;
            public string EntryCountText { get; set; } = string.Empty;
            public int EntryCount { get; set; }
        }
    }
}
