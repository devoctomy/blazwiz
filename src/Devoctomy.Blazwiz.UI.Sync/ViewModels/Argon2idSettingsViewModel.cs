using System.Security.Cryptography;
using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Devoctomy.Blazwiz.UI.Text.Interfaces;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.UI.Sync.ViewModels;

/// <summary>
/// ViewModel for the Argon2id settings panel: shows the settings new passphrase keys are derived with, and times one
/// derivation on this device.
/// </summary>
public partial class Argon2idSettingsViewModel : ViewModelBase
{
    private readonly IArgon2idKeyDerivationService argon2;
    private readonly Argon2idOptions options;
    private readonly TimeProvider timeProvider;
    private readonly IStringLocalizer<SyncUiText> text;
    private readonly IErrorText errorText;

    private bool isRunning;
    private string? result;
    private string? error;

    /// <summary>
    /// Initializes a new instance of the <see cref="Argon2idSettingsViewModel"/> class.
    /// </summary>
    /// <param name="argon2">Derives the keys.</param>
    /// <param name="options">The settings for new keys.</param>
    /// <param name="timeProvider">Times the benchmark.</param>
    /// <param name="text">The sync UI's text.</param>
    /// <param name="errorText">Describes errors.</param>
    public Argon2idSettingsViewModel(IArgon2idKeyDerivationService argon2, Argon2idOptions options, TimeProvider timeProvider, IStringLocalizer<SyncUiText> text, IErrorText errorText)
    {
        this.argon2 = argon2 ?? throw new ArgumentNullException(nameof(argon2));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));
    }

    /// <summary>
    /// Gets the settings new passphrase keys are derived with.
    /// </summary>
    public Argon2idParameters Parameters => options.Parameters;

    /// <summary>
    /// Gets the memory, e.g. "512 MiB".
    /// </summary>
    public string MemoryText => Parameters.MemoryKib % 1024 == 0
        ? text["Sync.Argon2id.MemoryMib", Parameters.MemoryKib / 1024]
        : text["Sync.Argon2id.MemoryKib", Parameters.MemoryKib];

    /// <summary>
    /// Gets a value indicating whether the benchmark is running.
    /// </summary>
    public bool IsRunning
    {
        get => isRunning;
        private set => SetProperty(ref isRunning, value);
    }

    /// <summary>
    /// Gets how long the last benchmark took, e.g. "Took 2.4 s", or null.
    /// </summary>
    public string? Result
    {
        get => result;
        private set => SetProperty(ref result, value);
    }

    /// <summary>
    /// Gets why the last benchmark failed, or null.
    /// </summary>
    public string? Error
    {
        get => error;
        private set => SetProperty(ref error, value);
    }

    /// <summary>
    /// Derives one key with the settings, from a random passphrase and salt, and shows how long it took.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task BenchmarkAsync()
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        Result = null;
        Error = null;
        try
        {
            var passphrase = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var salt = RandomNumberGenerator.GetBytes(16);
            var started = timeProvider.GetTimestamp();
            var key = await argon2.DeriveKeyAsync(passphrase, salt, Parameters.Clone());
            var elapsed = timeProvider.GetElapsedTime(started);
            CryptographicOperations.ZeroMemory(key);
            Result = text["Sync.Argon2id.Took", elapsed.TotalSeconds];
        }
        catch (Exception ex)
        {
            Error = text["Sync.Argon2id.Failed", errorText.Describe(ex)];
        }
        finally
        {
            IsRunning = false;
        }
    }
}
