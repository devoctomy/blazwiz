namespace blazwiz.Core.Text;

/// <summary>
/// The plural forms languages use, as in the Unicode CLDR plural rules. English uses <see cref="One"/> and
/// <see cref="Other"/>; other languages use more. Text that depends on a count has one resource per form, named
/// <c>&lt;key&gt;.&lt;form&gt;</c> (e.g. <c>Sync.Storage.FileCount.One</c>).
/// </summary>
public enum PluralCategory
{
    /// <summary>
    /// Zero (e.g. Arabic, Latvian).
    /// </summary>
    Zero,

    /// <summary>
    /// One (singular).
    /// </summary>
    One,

    /// <summary>
    /// Two (e.g. Arabic, Welsh).
    /// </summary>
    Two,

    /// <summary>
    /// Few (e.g. Polish, Czech).
    /// </summary>
    Few,

    /// <summary>
    /// Many (e.g. Polish, Russian).
    /// </summary>
    Many,

    /// <summary>
    /// Every other count. Every count-dependent text has this form.
    /// </summary>
    Other,
}
