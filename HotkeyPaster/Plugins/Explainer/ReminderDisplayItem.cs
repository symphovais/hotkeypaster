using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TalkKeys.Services.Auth;

namespace TalkKeys.Plugins.Explainer
{
    /// <summary>
    /// View model for displaying a reminder/calendar event in the checkbox list.
    /// </summary>
    public class ReminderDisplayItem : INotifyPropertyChanged
    {
        private bool _isSelected = true; // Default checked

        public CalendarEvent Event { get; }

        public ReminderDisplayItem(CalendarEvent calendarEvent)
        {
            Event = calendarEvent;
        }

        /// <summary>
        /// Whether this reminder is selected for export
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Event title for display
        /// </summary>
        public string Title => Event.Title;

        /// <summary>
        /// Formatted date/time for display
        /// </summary>
        public string DateTimeDisplay
        {
            get
            {
                if (Event.AllDay)
                {
                    return Event.Start.ToString("ddd, MMM d");
                }
                return Event.Start.ToString("ddd, MMM d @ h:mm tt");
            }
        }

        /// <summary>
        /// Location for display (may be null)
        /// </summary>
        public string? LocationDisplay => Event.Location;

        /// <summary>
        /// Whether to show location
        /// </summary>
        public bool HasLocation => !string.IsNullOrEmpty(Event.Location);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
