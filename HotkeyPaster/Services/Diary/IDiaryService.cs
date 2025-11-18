using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TalkKeys.Services.Diary
{
    /// <summary>
    /// Service for managing diary entries with daily markdown files
    /// </summary>
    public interface IDiaryService
    {
        /// <summary>
        /// Adds a new diary entry and saves it to the daily file
        /// </summary>
        /// <param name="text">The diary entry text</param>
        /// <param name="wordCount">Optional word count</param>
        /// <param name="language">Optional detected language</param>
        /// <returns>The created diary entry</returns>
        Task<DiaryEntry> AddEntryAsync(string text, int wordCount = 0, string? language = null);

        /// <summary>
        /// Gets all diary entries for a specific date
        /// </summary>
        /// <param name="date">The date to retrieve entries for</param>
        /// <returns>List of diary entries for that date</returns>
        Task<IEnumerable<DiaryEntry>> GetEntriesAsync(DateTime date);

        /// <summary>
        /// Searches diary entries within a date range
        /// </summary>
        /// <param name="query">Search text (case-insensitive)</param>
        /// <param name="fromDate">Start date (inclusive), null for all time</param>
        /// <param name="toDate">End date (inclusive), null for today</param>
        /// <returns>Matching diary entries</returns>
        Task<IEnumerable<DiaryEntry>> SearchEntriesAsync(string query, DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Gets the directory where diary files are stored
        /// </summary>
        string GetDiaryDirectory();

        /// <summary>
        /// Gets all dates that have diary entries
        /// </summary>
        Task<IEnumerable<DateTime>> GetDatesWithEntriesAsync();
    }
}
