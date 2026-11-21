using System.Runtime.CompilerServices;
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
/// ViewModel for the Argon2id benchmark: times one derivation on this device with the values entered, which start at
/// the parameters the app derives passphrase keys with. The benchmark never changes those parameters.
/// </summary>
public partial class Argon2idBenchmarkViewModel : ViewModelBase
{
    /// <summary>
    /// The shortest key the benchmark accepts, in bytes.
    /// </summary>
    public const int MinKeyLength = 16;

    private static readonly int[] CommonMemorySizesMib = [16, 19, 32, 64, 128, 256, 512];

    private readonly IArgon2idKeyDerivationService argon2;
    private readonly Argon2idOptions options;
    private readonly TimeProvider timeProvider;
    private readonly IStringLocalizer<SyncUiText> text;
    private readonly IErrorText errorText;

    private double memoryMib;
    private int iterations;
    private int parallelism;
    private int keyLength;
    private bool isRunning;
    private bool isMemoryMenuOpen;
    private string? result;
    private string? error;

    /// <summary>
    /// Initializes a new instance of the <see cref="Argon2idBenchmarkViewModel"/> class.
    /// </summary>
    /// <param name="argon2">Derives the keys.</param>
    /// <param name="options">The parameters the app derives passphrase keys with; only read.</param>
    /// <param name="timeProvider">Times the benchmark.</param>
    /// <param name="text">The sync UI's text.</param>
    /// <param name="errorText">Describes errors.</param>
    public Argon2idBenchmarkViewModel(IArgon2idKeyDerivationService argon2, Argon2idOptions options, TimeProvider timeProvider, IStringLocalizer<SyncUiText> text, IErrorText errorText)
    {
        this.argon2 = argon2 ?? throw new ArgumentNullException(nameof(argon2));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.errorText = errorText ?? throw new ArgumentNullException(nameof(errorText));

        memoryMib = options.Parameters.MemoryKib / 1024.0;
        iterations = options.Parameters.Iterations;
        parallelism = options.Parameters.Parallelism;
        keyLength = options.Parameters.KeyLength;
    }

    /// <summary>
    /// Gets the parameters the app derives passphrase keys with.
    /// </summary>
    public Argon2idParameters Parameters => options.Parameters;

    /// <summary>
    /// Gets the common memory sizes to choose from, in MiB.
    /// </summary>
    public IReadOnlyList<int> CommonMemoryMib => CommonMemorySizesMib;

    /// <summary>
    /// Gets a value indicating whether the menu of common memory sizes is open.
    /// </summary>
    public bool IsMemoryMenuOpen
    {
        get => isMemoryMenuOpen;
        private set => SetProperty(ref isMemoryMenuOpen, value);
    }

    /// <summary>
    /// Gets or sets the memory the benchmark uses, in MiB. Starts at the app's.
    /// </summary>
    public double MemoryMib
    {
        get => memoryMib;
        set => SetBenchmarkValue(ref memoryMib, value);
    }

    /// <summary>
    /// Gets or sets the number of passes the benchmark makes. Starts at the app's.
    /// </summary>
    public int Iterations
    {
        get => iterations;
        set => SetBenchmarkValue(ref iterations, value);
    }

    /// <summary>
    /// Gets or sets the number of lanes the benchmark uses. Starts at the app's.
    /// </summary>
    public int Parallelism
    {
        get => parallelism;
        set => SetBenchmarkValue(ref parallelism, value);
    }

    /// <summary>
    /// Gets or sets the length of the key the benchmark derives, in bytes. Starts at the app's.
    /// </summary>
    public int KeyLength
    {
        get => keyLength;
        set => SetBenchmarkValue(ref keyLength, value);
    }

    /// <summary>
    /// Gets why the values entered cannot be benchmarked, or null when they can.
    /// </summary>
    public string? Invalid
    {
        get
        {
            if (Iterations < 1)
            {
                return text["Sync.Argon2id.InvalidIterations"];
            }

            if (Parallelism < 1)
            {
                return text["Sync.Argon2id.InvalidParallelism"];
            }

            var minMemoryKib = 8L * Parallelism;
            if (MemoryKib < minMemoryKib)
            {
                return text["Sync.Argon2id.InvalidMemory", minMemoryKib];
            }

            if (KeyLength < MinKeyLength)
            {
                return text["Sync.Argon2id.InvalidKeyLength", MinKeyLength];
            }

            return null;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the benchmark can run: it is not running, and the values entered are valid.
    /// </summary>
    public bool CanBenchmark => !IsRunning && Invalid == null;

    /// <summary>
    /// Gets a value indicating whether the benchmark is running.
    /// </summary>
    public bool IsRunning
    {
        get => isRunning;
        private set
        {
            if (SetProperty(ref isRunning, value))
            {
                OnPropertyChanged(nameof(CanBenchmark));
            }
        }
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

    private double MemoryKib => Math.Round(MemoryMib * 1024);

    /// <summary>
    /// Opens or closes the menu of common memory sizes.
    /// </summary>
    [RelayCommand]
    public void ToggleMemoryMenu()
    {
        IsMemoryMenuOpen = !IsMemoryMenuOpen;
    }

    /// <summary>
    /// Closes the menu of common memory sizes.
    /// </summary>
    [RelayCommand]
    public void CloseMemoryMenu()
    {
        IsMemoryMenuOpen = false;
    }

    /// <summary>
    /// Sets the memory to one of the common sizes, and closes the menu.
    /// </summary>
    /// <param name="mib">The size, in MiB.</param>
    [RelayCommand]
    public void ChooseMemory(int mib)
    {
        MemoryMib = mib;
        IsMemoryMenuOpen = false;
    }

    /// <summary>
    /// Derives one key with the values entered, from a random passphrase and salt, and shows how long it took.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    public async Task BenchmarkAsync()
    {
        if (!CanBenchmark)
        {
            return;
        }

        IsRunning = true;
        Result = null;
        Error = null;
        try
        {
            var parameters = new Argon2idParameters
            {
                MemoryKib = checked((int)MemoryKib),
                Iterations = Iterations,
                Parallelism = Parallelism,
                KeyLength = KeyLength,
            };
            var passphrase = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var salt = RandomNumberGenerator.GetBytes(16);
            var started = timeProvider.GetTimestamp();
            var key = await argon2.DeriveKeyAsync(passphrase, salt, parameters);
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

    private void SetBenchmarkValue<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            OnPropertyChanged(nameof(Invalid));
            OnPropertyChanged(nameof(CanBenchmark));
        }
    }
}
