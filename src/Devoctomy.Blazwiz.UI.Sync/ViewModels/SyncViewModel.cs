using System.Globalization;
using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// ViewModel for the Sync page: the sync status, Sync now and turning sync on or off, and the sync log with search, filter
/// chips, grouping by pass and by day, paging and live updates.
/// </summary>
public partial class SyncViewModel : ViewModelBase
{
    /// <summary>
    /// How many log entries are loaded at a time.
    /// </summary>
    public const int PageSize = 200;

    /// <summary>
    /// How long after typing stops the search runs.
    /// </summary>
    public static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(300);

    private readonly ISyncEventLog eventLog;
    private readonly ISyncEventPresenter eventPresenter;
    private readonly ISyncStatusPresenter statusPresenter;
    private readonly ISyncService syncService;
    private readonly IConnectivityService connectivityService;
    private readonly ISyncScheduler syncScheduler;
    private readonly TimeProvider timeProvider;
    private readonly IStringLocalizer<SyncUiText> text;
    private readonly HashSet<string> expandedRows = [];
    private readonly HashSet<string> collapsedRows = [];

    private string searchText = string.Empty;
    private SyncEventGroup? activeGroup;
    private string? activeProfile;
    private int pages = 1;
    private IReadOnlyList<SyncLogDay> days = [];
    private bool hasMore;
    private bool isLoading = true;
    private bool isClearDialogOpen;
    private CancellationTokenSource? searchDebounce;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncViewModel"/> class.
    /// </summary>
    /// <param name="eventLog">The sync log.</param>
    /// <param name="eventPresenter">Maps log entries to icons, labels and groups.</param>
    /// <param name="statusPresenter">Describes the sync status.</param>
    /// <param name="syncService">The sync service.</param>
    /// <param name="connectivityService">The connectivity service.</param>
    /// <param name="syncScheduler">The sync scheduler.</param>
    /// <param name="timeProvider">The clock, time zone and timers.</param>
    /// <param name="text">The sync UI's text.</param>
    public SyncViewModel(
        ISyncEventLog eventLog,
        ISyncEventPresenter eventPresenter,
        ISyncStatusPresenter statusPresenter,
        ISyncService syncService,
        IConnectivityService connectivityService,
        ISyncScheduler syncScheduler,
        TimeProvider timeProvider,
        IStringLocalizer<SyncUiText> text)
    {
        this.eventLog = eventLog ?? throw new ArgumentNullException(nameof(eventLog));
        this.eventPresenter = eventPresenter ?? throw new ArgumentNullException(nameof(eventPresenter));
        this.statusPresenter = statusPresenter ?? throw new ArgumentNullException(nameof(statusPresenter));
        this.syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        this.connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        this.syncScheduler = syncScheduler ?? throw new ArgumentNullException(nameof(syncScheduler));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.text = text ?? throw new ArgumentNullException(nameof(text));

        this.eventLog.EventsWritten += OnEventsWritten;
        this.syncService.StatusChanged += OnSyncStatusChanged;
        this.connectivityService.StatusChanged += OnConnectivityChanged;
    }

    /// <summary>
    /// Gets the sync state, as the status pill shows it.
    /// </summary>
    public SyncStatusPresentation Status => statusPresenter.Present(syncService.Status, connectivityService.Status);

    /// <summary>
    /// Gets the sync status: last synced time and pending changes.
    /// </summary>
    public SyncStatus SyncStatus => syncService.Status;

    /// <summary>
    /// Gets a value indicating whether sync is turned on.
    /// </summary>
    public bool IsSyncEnabled => connectivityService.IsSyncEnabled;

    /// <summary>
    /// Gets a value indicating whether Sync now can be used.
    /// </summary>
    public bool CanSyncNow => connectivityService.Status.IsOnline && syncService.Status.State != SyncState.Syncing;

    /// <summary>
    /// Gets or sets the search text. Matches the message, file key and profile; runs shortly after typing stops.
    /// </summary>
    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value ?? string.Empty))
            {
                ScheduleSearch();
            }
        }
    }

    /// <summary>
    /// Gets the filter chips: All, then one per group.
    /// </summary>
    public IReadOnlyList<SyncFilterChip> Chips =>
        [
            new SyncFilterChip { Group = null, Label = eventPresenter.DescribeGroup(null), IsActive = activeGroup == null },
            .. Enum.GetValues<SyncEventGroup>().Select(g => new SyncFilterChip { Group = g, Label = eventPresenter.DescribeGroup(g), IsActive = activeGroup == g }),
        ];

    /// <summary>
    /// Gets the selected group, or null for All.
    /// </summary>
    public SyncEventGroup? ActiveGroup => activeGroup;

    /// <summary>
    /// Gets the configured profiles.
    /// </summary>
    public IReadOnlyList<string> Profiles => syncService.Profiles;

    /// <summary>
    /// Gets a value indicating whether to show the profile filter (only with more than one profile).
    /// </summary>
    public bool ShowProfileFilter => Profiles.Count > 1;

    /// <summary>
    /// Gets the selected profile, or null for all.
    /// </summary>
    public string? ActiveProfile => activeProfile;

    /// <summary>
    /// Gets the log, newest first, grouped by day.
    /// </summary>
    public IReadOnlyList<SyncLogDay> Days
    {
        get => days;
        private set => SetProperty(ref days, value);
    }

    /// <summary>
    /// Gets a value indicating whether more entries can be loaded.
    /// </summary>
    public bool HasMore
    {
        get => hasMore;
        private set => SetProperty(ref hasMore, value);
    }

    /// <summary>
    /// Gets a value indicating whether the log is loading for the first time.
    /// </summary>
    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    /// <summary>
    /// Gets a value indicating whether the dialog confirming the log should be cleared is showing.
    /// </summary>
    public bool IsClearDialogOpen
    {
        get => isClearDialogOpen;
        private set => SetProperty(ref isClearDialogOpen, value);
    }

    /// <inheritdoc/>
    public override async Task OnInitializedAsync()
    {
        await connectivityService.InitializeAsync();
        RefreshStatus();
        await LoadSafelyAsync();
        IsLoading = false;
    }

    /// <summary>
    /// Shows one group of entries, or all of them.
    /// </summary>
    /// <param name="group">The group, or null for All.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SelectGroupAsync(SyncEventGroup? group)
    {
        activeGroup = group;
        pages = 1;
        OnPropertyChanged(nameof(Chips));
        OnPropertyChanged(nameof(ActiveGroup));
        await LoadSafelyAsync();
    }

    /// <summary>
    /// Shows one profile's entries, or every profile's.
    /// </summary>
    /// <param name="profile">The profile, or null for all.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SelectProfileAsync(string? profile)
    {
        activeProfile = string.IsNullOrEmpty(profile) ? null : profile;
        pages = 1;
        OnPropertyChanged(nameof(ActiveProfile));
        await LoadSafelyAsync();
    }

    /// <summary>
    /// Loads the next page of entries.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        pages++;
        await LoadSafelyAsync();
    }

    /// <summary>
    /// Expands or collapses a pass to show its file entries.
    /// </summary>
    /// <param name="rowId">The row id.</param>
    [RelayCommand]
    public void ToggleRow(string rowId)
    {
        var row = Days.SelectMany(d => d.Rows).FirstOrDefault(r => r.Id == rowId);
        if (row == null)
        {
            return;
        }

        if (row.IsExpanded)
        {
            expandedRows.Remove(rowId);
            collapsedRows.Add(rowId);
        }
        else
        {
            collapsedRows.Remove(rowId);
            expandedRows.Add(rowId);
        }

        row.IsExpanded = !row.IsExpanded;
        Days = [.. Days];
    }

    /// <summary>
    /// Syncs every profile now.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task SyncNowAsync()
    {
        if (CanSyncNow)
        {
            await syncScheduler.SyncNowAsync();
        }
    }

    /// <summary>
    /// Turns sync on or off. It is remembered until switched back.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ToggleSyncAsync()
    {
        await connectivityService.SetSyncEnabledAsync(!connectivityService.IsSyncEnabled);
        RefreshStatus();
    }

    /// <summary>
    /// Asks for confirmation before clearing the log.
    /// </summary>
    [RelayCommand]
    public void ShowClearDialog()
    {
        IsClearDialogOpen = true;
    }

    /// <summary>
    /// Closes the clear log dialog without clearing.
    /// </summary>
    [RelayCommand]
    public void CloseClearDialog()
    {
        IsClearDialogOpen = false;
    }

    /// <summary>
    /// Clears the signed in user's sync log.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task ClearLogAsync()
    {
        IsClearDialogOpen = false;
        await eventLog.ClearAsync();
        pages = 1;
        await LoadSafelyAsync();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            eventLog.EventsWritten -= OnEventsWritten;
            syncService.StatusChanged -= OnSyncStatusChanged;
            connectivityService.StatusChanged -= OnConnectivityChanged;
            searchDebounce?.Cancel();
            searchDebounce?.Dispose();
            searchDebounce = null;
        }

        base.Dispose(disposing);
    }

    private static bool IsPassEvent(SyncEvent entry) => entry.Kind is SyncEventKind.PassStarted or SyncEventKind.PassCompleted;

    private async Task LoadSafelyAsync()
    {
        try
        {
            await LoadAsync();
        }
        catch (Exception)
        {
            // The log is diagnostic only; keep showing what was loaded.
        }
    }

    private async Task LoadAsync()
    {
        var query = new SyncEventQuery
        {
            Profile = activeProfile,
            Kinds = activeGroup == null ? null : eventPresenter.GetKinds(activeGroup.Value),
            Text = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
            Describe = eventPresenter.Describe,
            Take = pages * PageSize,
        };

        var page = await eventLog.QueryAsync(query);
        HasMore = page.HasMore;

        // Passes are grouped with their file entries; when filtering or searching, each match is shown on its own.
        var grouped = activeGroup == null && query.Text == null;
        Days = BuildDays(page.Events, grouped);
    }

    private List<SyncLogDay> BuildDays(IReadOnlyList<SyncEvent> events, bool grouped)
    {
        var rows = grouped
            ? events.GroupBy(e => e.PassId ?? e.Id).Select(g => BuildPassRow(g.Key, g.ToList()))
            : events.Select(e => BuildRow(e.Id, Present(e), []));

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        return rows
            .OrderByDescending(r => r.Header.Timestamp)
            .GroupBy(r => DateOnly.FromDateTime(ToLocal(r.Header.Timestamp).DateTime))
            .Select(g => new SyncLogDay
            {
                Date = g.Key,
                Label = g.Key == today
                    ? text["Sync.Log.Today"]
                    : g.Key == today.AddDays(-1) ? text["Sync.Log.Yesterday"] : g.Key.ToString(text["Sync.Log.DayFormat"], CultureInfo.CurrentCulture),
                Rows = g.ToList(),
            })
            .ToList();
    }

    private SyncLogRow BuildPassRow(string id, List<SyncEvent> events)
    {
        var completed = events.FirstOrDefault(e => e.Kind == SyncEventKind.PassCompleted);
        var started = events.FirstOrDefault(e => e.Kind == SyncEventKind.PassStarted);
        var headerEvent = completed ?? started ?? events[0];
        var header = Present(headerEvent);
        if (completed != null && !completed.IsNoChangePass)
        {
            header.Message = eventPresenter.DescribePass(completed);
        }
        else if (completed == null && started != null)
        {
            header.Message = eventPresenter.DescribePass(null);
        }

        var children = events
            .Where(e => e != headerEvent && !IsPassEvent(e))
            .OrderBy(e => e.Timestamp)
            .Select(Present)
            .ToList();
        return BuildRow(id, header, children);
    }

    private SyncLogRow BuildRow(string id, SyncLogEntry header, IReadOnlyList<SyncLogEntry> children)
    {
        var hasErrors = header.Presentation.Group == SyncEventGroup.Errors || children.Any(c => c.Presentation.Group == SyncEventGroup.Errors);
        return new SyncLogRow
        {
            Id = id,
            Header = header,
            Children = children,
            HasErrors = hasErrors,

            // Errors are expanded by default, unless the user collapsed them.
            IsExpanded = expandedRows.Contains(id) || (hasErrors && !collapsedRows.Contains(id)),
        };
    }

    private SyncLogEntry Present(SyncEvent entry)
    {
        var local = ToLocal(entry.Timestamp);
        return new SyncLogEntry
        {
            Id = entry.Id,
            Presentation = eventPresenter.Present(entry.Kind),
            Message = eventPresenter.Describe(entry),
            Key = entry.Key,
            Profile = entry.Profile,
            Detail = entry.Detail,
            Timestamp = entry.Timestamp,
            RelativeTime = DescribeAge(timeProvider.GetUtcNow() - entry.Timestamp),
            ExactTime = local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Count = entry.Count,
        };
    }

    private DateTimeOffset ToLocal(DateTimeOffset timestamp) => TimeZoneInfo.ConvertTime(timestamp, timeProvider.LocalTimeZone);

    private string DescribeAge(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
        {
            return text["Sync.Log.Age.JustNow"];
        }

        if (age < TimeSpan.FromHours(1))
        {
            return text["Sync.Log.Age.Minutes", (int)age.TotalMinutes];
        }

        return age < TimeSpan.FromDays(1) ? text["Sync.Log.Age.Hours", (int)age.TotalHours] : text["Sync.Log.Age.Days", (int)age.TotalDays];
    }

    private void ScheduleSearch()
    {
        searchDebounce?.Cancel();
        searchDebounce?.Dispose();
        searchDebounce = new CancellationTokenSource();
        _ = SearchAfterDelayAsync(searchDebounce.Token);
    }

    private async Task SearchAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(SearchDebounce, timeProvider, ct);
            pages = 1;
            await LoadSafelyAsync();
        }
        catch (OperationCanceledException)
        {
            // More typing restarted the delay.
        }
    }

    private void RefreshStatus()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(SyncStatus));
        OnPropertyChanged(nameof(IsSyncEnabled));
        OnPropertyChanged(nameof(CanSyncNow));
    }

    private async void OnEventsWritten(object? sender, EventArgs e)
    {
        await LoadSafelyAsync();
    }

    private void OnSyncStatusChanged(object? sender, SyncStatus e)
    {
        RefreshStatus();
    }

    private void OnConnectivityChanged(object? sender, ConnectivityStatus e)
    {
        RefreshStatus();
    }
}
